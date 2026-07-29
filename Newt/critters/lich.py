import random

from .critter import Critter, LAND_TERRAINS


class Lich(Critter):
    """A player-summoned necromancer that raises a protective undead army."""

    ALLOWED_TERRAINS = LAND_TERRAINS
    DISPLACEMENT_LEVEL = 4
    HUNT_RANGE = 12
    PLAYER_SPAWN_ONLY = True
    PREDATOR_NAME = "Lich"

    def __init__(self, x, y):
        super().__init__(
            x,
            y,
            color=(105, 75, 145),
            allowed_terrains=self.ALLOWED_TERRAINS,
            move_cooldown=0.42,
            sprite="lich",
        )

    def get_hunt_prey_types(self):
        from . import CRITTER_TYPES

        return tuple(CRITTER_TYPES.values())

    def is_valid_hunt_prey(self, critter, prey_types):
        from .ape import Ape
        from .deer import Deer
        from .giga_slug import GigaSlug
        from .land_kraken import LandKraken
        from .mega_spider import MegaSpider
        from .sand_worm import SandWorm
        from .therapsid import Therapsid
        from .wolf import Wolf

        if (
            critter.current_behavior == "dying"
            or isinstance(critter, (Lich, Undead, UndeadBeast))
        ):
            return False

        if isinstance(critter, Ape):
            return True

        return isinstance(
            critter,
            (
                Deer,
                GigaSlug,
                LandKraken,
                MegaSpider,
                SandWorm,
                Therapsid,
                Wolf,
            ),
        )

    def can_be_hunted_by(self, predator):
        from .ape_warrior import ApeWarrior

        return isinstance(predator, ApeWarrior)

    def can_displace_critter(self, critter):
        return True

    def should_remove_on_failed_displacement(self, critter):
        return False

    def resolve_hunt_attack(self, game, prey, predator_name=None):
        from .ape import Ape
        from .dog import Dog

        undead_type = (
            Undead
            if isinstance(prey, Ape) and not isinstance(prey, Dog)
            else UndeadBeast
        )
        undead_type.raise_from(game, prey, self)
        self.set_behavior("raise_undead")
        return False

    def try_handle_priority_behavior(self, game):
        return self.hunt_nearest_prey(
            game,
            self.get_hunt_prey_types(),
            self.get_predator_name(),
        )


class UndeadFollower(Critter):
    """Shared following and protection behavior for a lich's converts."""

    ALLOWED_TERRAINS = LAND_TERRAINS
    DISPLACEMENT_LEVEL = 2
    FOLLOW_DISTANCE = 4
    PROTECTION_RANGE = 12
    APE_ATTACK_CHANCE = 0.05
    PREDATOR_NAME = "Undead"

    COLOR = (115, 135, 105)
    MOVE_COOLDOWN = 0.34
    SPRITE = "undead"

    def __init__(self, x, y, master_lich=None):
        super().__init__(
            x,
            y,
            color=self.COLOR,
            allowed_terrains=self.ALLOWED_TERRAINS,
            move_cooldown=self.MOVE_COOLDOWN,
            sprite=self.SPRITE,
        )
        self.master_lich = master_lich

    @classmethod
    def raise_from(cls, game, critter, master_lich):
        occupied_positions = tuple(critter.get_occupied_positions())
        critter.clear_home_building()

        for x, y in occupied_positions:
            if (x, y) == (critter.x, critter.y):
                continue
            tile = game.world.get_tile(x, y)
            if tile is not None and tile.critter is critter:
                tile.critter = None

        critter.__class__ = cls
        critter.color = cls.COLOR
        critter.sprite = cls.SPRITE
        critter.allowed_terrains = set(cls.ALLOWED_TERRAINS)
        critter.required_tags = set()
        critter.move_cooldown = cls.MOVE_COOLDOWN
        critter.move_timer = 0.0
        critter.master_lich = master_lich
        critter.home_building = None
        critter.trapped_by_web = None
        critter.needs_habitat_relocation = False
        critter.is_hungry = False
        critter.hunger_interval = None
        critter.starvation_interval = None
        critter.hunger_timer = None
        critter.starvation_timer = None
        critter.dying_timer = None
        critter.meals_eaten = 0
        if hasattr(critter, "carrying_food"):
            del critter.carrying_food
        critter.set_behavior("raised_undead")
        game.dying_critters.discard(critter)
        return critter

    def get_hunt_range(self):
        return self.PROTECTION_RANGE

    def get_hunt_prey_types(self):
        from .ape_warrior import ApeWarrior

        return (ApeWarrior,)

    def get_opportunistic_ape_prey_types(self):
        from .ape import Ape

        return (Ape,)

    def get_scavenge_prey_types(self):
        return ()

    def handle_successful_meal(self, game, meal_points=None):
        self.meals_eaten = 0
        self.set_behavior("protect_lich")
        return None

    def get_active_master(self, game):
        master = self.master_lich
        if isinstance(master, Lich):
            tile = game.world.get_tile(master.x, master.y)
            if (
                tile is not None
                and tile.critter is master
                and master.current_behavior != "dying"
            ):
                return master

        liches = [
            critter
            for critter in game.critters
            if (
                isinstance(critter, Lich)
                and critter.current_behavior != "dying"
                and game.world.get_tile(critter.x, critter.y) is not None
                and game.world.get_tile(critter.x, critter.y).critter is critter
            )
        ]
        self.master_lich = min(
            liches,
            key=lambda lich: self.get_tile_distance(
                game.world,
                self.x,
                self.y,
                lich.x,
                lich.y,
            ),
            default=None,
        )
        return self.master_lich

    def follow_master(self, game, master):
        if (
            self.get_tile_distance(
                game.world,
                self.x,
                self.y,
                master.x,
                master.y,
            )
            <= self.FOLLOW_DISTANCE
        ):
            self.set_behavior("guard_lich")
            return False

        path = self.find_path_to_nearest_position(
            game.world,
            {(master.x, master.y)},
            allow_occupied_target=True,
        )
        if not path:
            self.set_behavior("seek_lich")
            return False

        self.set_behavior("follow_lich")
        next_x, next_y = path[0]
        return self.move_to(game.world, next_x, next_y, game)

    def try_handle_priority_behavior(self, game):
        master = self.get_active_master(game)
        if master is None:
            self.set_behavior("masterless")
            return False

        if self.hunt_nearest_prey(
            game,
            self.get_hunt_prey_types(),
            self.get_predator_name(),
        ):
            return True

        if (
            random.random() < self.APE_ATTACK_CHANCE
            and self.hunt_nearest_prey(
                game,
                self.get_opportunistic_ape_prey_types(),
                self.get_predator_name(),
            )
        ):
            return True

        return self.follow_master(game, master)


class Undead(UndeadFollower):
    """An ape-derived member of a lich's undead guard."""


class UndeadBeast(UndeadFollower):
    """A beast-derived member of a lich's undead guard."""

    COLOR = (95, 125, 90)
    MOVE_COOLDOWN = 0.30
    SPRITE = "undead_beast"
    PREDATOR_NAME = "Undead Beast"
