import random

from .ape import Ape


class ApeWarrior(Ape):
    """A village defender recruited to hunt threats to ape settlements."""

    CRITTER_TAGS = frozenset({"animal", "terrestrial", "vertebrate", "sapient", "warrior"})
    COMBAT_CAPABLE = True
    COMBAT_POWER = 3
    MAX_COMBAT_HEALTH = 3
    DEER_TAMING_CHANCE = 0.20
    PREDATOR_NAME = "Ape Warrior"
    STARVATION_INTERVAL = 120.0
    CAN_CROSS_TERRAIN_DURING_WAR = True

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = (145, 85, 65)
        self.sprite = "ape_warrior"
        self.configure_hunger(self.HUNGER_INTERVAL, self.STARVATION_INTERVAL)
        self.deer_taming_contact_ids = set()

    @classmethod
    def recruit(cls, ape):
        if not Ape.is_recruitable_civilian(ape):
            return None

        ape.__class__ = cls
        ape.color = (145, 85, 65)
        ape.sprite = "ape_warrior"
        ape.configure_combat()
        ape.configure_hunger(cls.HUNGER_INTERVAL, cls.STARVATION_INTERVAL)
        ape.deer_taming_contact_ids = set()
        ape.set_behavior("recruited")
        return ape

    def get_hunt_prey_types(self):
        from .land_kraken import LandKraken
        from .lich import Lich, UndeadFollower
        from .mega_spider import MegaSpider
        from .wolf import Wolf
        from .tyrannosaurus import Tyrannosaurus
        from .herrera import Herrera
        from .smasher import Smasher

        return (LandKraken, Lich, MegaSpider, UndeadFollower, Wolf, Tyrannosaurus, Herrera, Smasher)

    def get_priority_hunt_prey_types(self):
        from .lich import UndeadFollower

        return (UndeadFollower,)

    def has_active_village_war(self):
        from city import City

        return (
            self.CAN_CROSS_TERRAIN_DURING_WAR
            and isinstance(self.home_building, City)
            and bool(self.home_building.war_enemies)
        )

    def is_habitable_tile(self, tile):
        if self.has_active_village_war():
            return tile is not None
        return super().is_habitable_tile(tile)

    def update(self, game, dt):
        tile = game.world.get_tile(self.x, self.y)
        if (
            not self.has_active_village_war()
            and not super().is_habitable_tile(tile)
        ):
            self.needs_habitat_relocation = True
        super().update(game, dt)

    def get_scavenge_prey_types(self):
        if self.carrying_food:
            return ()
        return self.get_hunt_prey_types()

    def create_offspring(self, x, y):
        return Ape(x, y)

    def is_enemy_village_ape(self, critter):
        from city import City
        from .dog import Dog

        village = self.home_building
        return (
            isinstance(village, City)
            and isinstance(critter, Ape)
            and not isinstance(critter, Dog)
            and critter.home_building in village.war_enemies
            and critter.current_behavior != "dying"
        )

    def should_feed_on_hunt_target(self, prey):
        if self.is_enemy_village_ape(prey):
            return False
        return super().should_feed_on_hunt_target(prey)

    def try_hunt_war_enemy(self, game):
        if type(self) not in (ApeWarrior, ApeCavalry):
            return False

        village = self.get_home_village(game.world)
        if village is None or not village.get_active_war_enemies(game.world):
            return False

        critter_type_index = getattr(game, "critter_type_index", None)
        if critter_type_index is None:
            ape_candidates = game.critters
        else:
            ape_candidates = [
                critter
                for critter_type, critters in critter_type_index.items()
                if issubclass(critter_type, Ape)
                for critter in critters
            ]

        target_positions = {
            position
            for critter in ape_candidates
            if self.is_enemy_village_ape(critter)
            for position in critter.get_occupied_positions()
        }
        if not target_positions:
            return False

        path = self.find_path_to_nearest_tile(
            game.world,
            lambda tile: (tile.x, tile.y) in target_positions,
            allow_occupied_target=True,
        )
        if not path:
            self.set_behavior("war_blocked")
            return False

        target_x, target_y = path[0]
        target_tile = game.world.get_tile(target_x, target_y)
        target = None if target_tile is None else target_tile.critter
        if target is not None and self.is_enemy_village_ape(target):
            if self.resolve_hunt_attack(game, target, self.get_predator_name()):
                self.move_to(game.world, target_x, target_y, game)
                self.set_behavior("war_kill")
            return True

        self.set_behavior("war_march")
        self.move_to(game.world, target_x, target_y, game)
        return True

    def try_tame_adjacent_deer(self, game):
        if type(self) is not ApeWarrior:
            return False

        if not hasattr(self, "deer_taming_contact_ids"):
            self.deer_taming_contact_ids = set()

        from .deer import Deer

        adjacent_deer = []
        for x, y in self.get_neighbor_positions(game.world, self.x, self.y):
            tile = game.world.get_tile(x, y)
            deer = None if tile is None else tile.critter
            if isinstance(deer, Deer) and deer.current_behavior != "dying":
                adjacent_deer.append(deer)

        adjacent_ids = {deer.id for deer in adjacent_deer}
        self.deer_taming_contact_ids.intersection_update(adjacent_ids)

        for deer in adjacent_deer:
            if deer.id in self.deer_taming_contact_ids:
                continue

            self.deer_taming_contact_ids.add(deer.id)
            if random.random() >= self.DEER_TAMING_CHANCE:
                continue

            if ApeCavalry.tame(self, deer, game) is not None:
                return True

        return False

    def try_handle_priority_behavior(self, game):
        village = self.get_home_village(game.world)
        if self.is_hungry and village is not None and village.food > 0:
            return self.consume_village_food(game, village)

        if self.try_hunt_war_enemy(game):
            return True

        if self.try_tame_adjacent_deer(game):
            return True

        if self.hunt_nearest_prey(
            game,
            self.get_priority_hunt_prey_types(),
            self.get_predator_name(),
        ):
            return True
        return self.try_handle_hunter_priority_behavior(game)


class ApeCavalry(ApeWarrior):
    CRITTER_TAGS = frozenset({"animal", "terrestrial", "vertebrate", "sapient", "warrior"})
    """A fast warrior mounted on a deer tamed in the field."""

    PREDATOR_NAME = "Ape Cavalry"
    MOVE_COOLDOWN = 0.16
    COLOR = (180, 135, 80)
    SPRITE = "ape_cavalry"

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = self.COLOR
        self.sprite = self.SPRITE
        self.move_cooldown = self.MOVE_COOLDOWN

    @classmethod
    def tame(cls, warrior, deer, game):
        from entity_cleanup import remove_critter

        if type(warrior) is not ApeWarrior or deer.current_behavior == "dying":
            return None

        remove_critter(game, deer, f"it became the mount of Ape Warrior {warrior.id}")
        warrior.__class__ = cls
        warrior.color = cls.COLOR
        warrior.sprite = cls.SPRITE
        warrior.move_cooldown = cls.MOVE_COOLDOWN
        warrior.move_timer = 0.0
        warrior.configure_combat()
        warrior.deer_taming_contact_ids = set()
        warrior.set_behavior("tame_deer")
        return warrior
