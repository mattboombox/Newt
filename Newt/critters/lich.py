import random

from .critter import Critter, LAND_TERRAINS


class Lich(Critter):
    """A player-summoned necromancer that raises a protective undead army."""

    ALLOWED_TERRAINS = LAND_TERRAINS
    COMBAT_CAPABLE = True
    COMBAT_POWER = 4
    MAX_COMBAT_HEALTH = 7
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
        from .messiah import Messiah
        from .sand_worm import SandWorm
        from .therapsid import Therapsid
        from .tyrannosaurus import Tyrannosaurus
        from .wolf import Wolf

        if (
            critter.current_behavior == "dying"
            or isinstance(critter, (Lich, UndeadFollower))
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
                Messiah,
                SandWorm,
                Therapsid,
                Tyrannosaurus,
                Wolf,
            ),
        )

    def can_be_hunted_by(self, predator):
        from .ape_warrior import ApeWarrior
        from .smasher import Smasher

        return isinstance(predator, (ApeWarrior, Smasher))

    def can_displace_critter(self, critter):
        return True

    def should_remove_on_failed_displacement(self, critter):
        return False

    def get_raised_undead_type(self, critter):
        from .ape import Ape
        from .ape_warrior import ApeCavalry
        from .dog import Dog
        from .tyrannosaurus import Tyrannosaurus

        if isinstance(critter, Tyrannosaurus):
            return UndeadTrex
        if isinstance(critter, ApeCavalry):
            return UndeadCavalry
        if isinstance(critter, Ape) and not isinstance(critter, Dog):
            return Undead
        return UndeadBeast

    def can_raise_dying_critter(self, game, critter):
        if (
            critter is self
            or critter not in game.critters
            or critter.current_behavior != "dying"
            or isinstance(critter, (Lich, UndeadFollower))
        ):
            return False

        tile = game.world.get_tile(critter.x, critter.y)
        return (
            tile is not None
            and tile.critter is critter
            and self.is_habitable_tile(tile)
        )

    def raise_dying_critter(self, game, critter):
        if not self.can_raise_dying_critter(game, critter):
            return False

        undead_type = self.get_raised_undead_type(critter)
        undead_type.raise_from(game, critter, self)
        self.set_behavior("raise_undead")
        return True

    def try_raise_dying_critter(self, game):
        dying_critters = getattr(game, "dying_critters", ())
        target_positions = {
            (critter.x, critter.y)
            for critter in dying_critters
            if self.can_raise_dying_critter(game, critter)
        }
        if not target_positions:
            return False

        path = self.find_path_to_nearest_tile(
            game.world,
            lambda tile: (tile.x, tile.y) in target_positions,
            allow_occupied_target=True,
            max_search_distance=self.get_hunt_range(),
        )
        if not path:
            return False

        target_x, target_y = path[0]
        target_tile = game.world.get_tile(target_x, target_y)
        target = None if target_tile is None else target_tile.critter
        if target is not None and self.raise_dying_critter(game, target):
            return True

        self.set_behavior("seek_dying")
        self.move_to(game.world, target_x, target_y, game)
        return True

    def resolve_noncombat_hunt_attack(
        self,
        game,
        prey,
        predator_name=None,
    ):
        undead_type = self.get_raised_undead_type(prey)
        undead_type.raise_from(game, prey, self)
        self.set_behavior("raise_undead")
        return False

    def resolve_defeated_combat_target(
        self,
        game,
        prey,
        predator_name=None,
    ):
        return self.resolve_noncombat_hunt_attack(
            game,
            prey,
            predator_name,
        )

    def try_handle_priority_behavior(self, game):
        if self.try_raise_dying_critter(game):
            return True

        return self.hunt_nearest_prey(
            game,
            self.get_hunt_prey_types(),
            self.get_predator_name(),
        )


class UndeadFollower(Critter):
    """Shared roaming and ape-hunting behavior for a lich's converts."""

    ALLOWED_TERRAINS = LAND_TERRAINS
    COMBAT_CAPABLE = True
    COMBAT_POWER = 2
    MAX_COMBAT_HEALTH = 2
    DISPLACEMENT_LEVEL = 2
    PROTECTION_RANGE = 12
    APE_ATTACK_CHANCE = 0.35
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
    def get_raised_sprite(cls):
        return cls.SPRITE

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
        critter.sprite = cls.get_raised_sprite()
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
        critter.configure_combat()
        if hasattr(critter, "carrying_food"):
            del critter.carrying_food
        critter.set_behavior("raised_undead")
        game.dying_critters.discard(critter)
        return critter

    def get_hunt_range(self):
        return self.PROTECTION_RANGE

    def get_hunt_prey_types(self):
        from .ape import Ape
        from .messiah import Messiah

        return (Ape, Messiah)

    def get_scavenge_prey_types(self):
        return ()

    def handle_successful_meal(self, game, meal_points=None):
        self.meals_eaten = 0
        self.set_behavior("attack_ape")
        return None

    def try_handle_priority_behavior(self, game):
        if (
            random.random() < self.APE_ATTACK_CHANCE
            and self.hunt_nearest_prey(
                game,
                self.get_hunt_prey_types(),
                self.get_predator_name(),
            )
        ):
            return True

        self.set_behavior("roam")
        return False


class Undead(UndeadFollower):
    """An ape-derived member of a lich's undead guard."""

    def try_mount_adjacent_undead_beast(self, game):
        if type(self) is not Undead:
            return False

        for x, y in self.get_neighbor_positions(game.world, self.x, self.y):
            tile = game.world.get_tile(x, y)
            beast = None if tile is None else tile.critter
            if (
                type(beast) is UndeadBeast
                and beast.current_behavior != "dying"
            ):
                return UndeadCavalry.mount(self, beast, game) is not None

        return False

    def try_handle_priority_behavior(self, game):
        if self.try_mount_adjacent_undead_beast(game):
            return True
        return super().try_handle_priority_behavior(game)


class UndeadBeast(UndeadFollower):
    """A beast-derived member of a lich's undead guard."""

    COLOR = (95, 125, 90)
    COMBAT_POWER = 3
    MAX_COMBAT_HEALTH = 3
    MOVE_COOLDOWN = 0.30
    SPRITE = "undead_beast"
    PREDATOR_NAME = "Undead Beast"


class UndeadTrex(UndeadBeast):
    """A Tyrannosaurus raised with its apex strength intact."""

    COLOR = (85, 110, 75)
    COMBAT_POWER = 5
    MAX_COMBAT_HEALTH = 7
    DISPLACEMENT_LEVEL = 4
    MOVE_COOLDOWN = 0.30
    SPRITE = "undead_trex"
    PREDATOR_NAME = "Undead T-Rex"

    @classmethod
    def get_raised_sprite(cls):
        return random.choice(("undead_trex", "undead_trex2"))


class UndeadCavalry(Undead):
    """A fast undead warrior mounted on an undead beast."""

    COLOR = (105, 115, 80)
    COMBAT_POWER = 3
    MAX_COMBAT_HEALTH = 3
    MOVE_COOLDOWN = 0.16
    SPRITE = "undead_cavalry"
    PREDATOR_NAME = "Undead Cavalry"

    @classmethod
    def mount(cls, undead, beast, game):
        from entity_cleanup import remove_critter

        if (
            type(undead) is not Undead
            or type(beast) is not UndeadBeast
            or beast.current_behavior == "dying"
        ):
            return None

        remove_critter(
            game,
            beast,
            f"it became the mount of Undead {undead.id}",
        )
        undead.__class__ = cls
        undead.color = cls.COLOR
        undead.sprite = cls.SPRITE
        undead.move_cooldown = cls.MOVE_COOLDOWN
        undead.move_timer = 0.0
        undead.configure_combat()
        undead.set_behavior("mount_undead_beast")
        return undead
