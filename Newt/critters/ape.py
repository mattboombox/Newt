import random

from .critter import CARDINAL_DIRECTIONS, Critter
from .therapsid import Therapsid


class Ape(Therapsid):
    """A broadly omnivorous land hunter descended from therapsids."""

    DISPLACEMENT_LEVEL = 3
    PREDATOR_NAME = "Ape"
    REPRODUCTION_MEAL_THRESHOLD = 5
    REPRODUCTION_BLOCKS_SET_BEHAVIOR = False
    REPRODUCTION_BLOCKS_RESET_MEALS = False
    VILLAGE_CLAIM_RANGE = 28
    VILLAGE_ROAM_RANGE = 16
    WOLF_TAMING_CHANCE = 0.05

    def __init__(self, x, y):
        Critter.__init__(
            self,
            x,
            y,
            color=(125, 95, 70),
            allowed_terrains=Ape.ALLOWED_TERRAINS,
            move_cooldown=0.28,
            sprite="ape",
        )
        self.configure_hunger(Ape.HUNGER_INTERVAL, Ape.STARVATION_INTERVAL)
        self.carrying_food = 0
        self.settlement_path = []
        self.settlement_path_village = None
        self.wolf_taming_contact_ids = set()

    @staticmethod
    def is_recruitable_civilian(critter):
        """Only an ordinary Ape may be converted into a specialist."""
        return type(critter) is Ape

    def get_hunt_prey_types(self):
        from . import CRITTER_TYPES
        from .land_kraken import LandKraken
        from .lich import Lich
        from .mega_spider import MegaSpider
        from .wolf import Wolf

        excluded_types = (Ape, LandKraken, Lich, MegaSpider, Wolf)
        return tuple(
            critter_type
            for critter_type in CRITTER_TYPES.values()
            if not issubclass(critter_type, excluded_types)
        )

    def get_scavenge_prey_types(self):
        if self.carrying_food:
            return ()
        return self.get_hunt_prey_types()

    def is_valid_hunt_prey(self, critter, prey_types):
        if (
            isinstance(critter, Ape)
            or (
                not self.COMBAT_CAPABLE
                and critter.COMBAT_CAPABLE
            )
        ):
            return False
        return super().is_valid_hunt_prey(critter, prey_types)

    def is_valid_scavenge_prey(self, critter, prey_types):
        if (
            isinstance(critter, Ape)
            or (
                not self.COMBAT_CAPABLE
                and critter.COMBAT_CAPABLE
            )
        ):
            return False
        return super().is_valid_scavenge_prey(critter, prey_types)

    def get_reproduction_blocking_types(self):
        # Village population capacity replaces neighbor-based reproduction
        # blocking for apes.
        return ()

    def is_returning_to_village(self):
        return (
            self.carrying_food > 0
            or self.current_behavior
            in {
                "return_food",
                "return_to_reproduce",
            }
        )

    def can_displace_critter(self, critter):
        from .dog import Dog

        if isinstance(critter, Dog):
            return True
        if self.is_returning_to_village():
            return True
        return super().can_displace_critter(critter)

    def should_remove_on_failed_displacement(self, critter):
        if isinstance(critter, Ape):
            return False
        if self.is_returning_to_village():
            return False
        return super().should_remove_on_failed_displacement(critter)

    def can_path_through_tile(self, tile):
        if (
            self.is_returning_to_village()
            and tile is not None
            and tile.critter is not None
            and self.is_habitable_tile(tile)
        ):
            return True
        return super().can_path_through_tile(tile)

    def get_home_village(self, world):
        from city import City

        village = self.home_building
        if not isinstance(village, City):
            return None

        tile = world.get_tile(village.x, village.y)
        if tile is None or tile.building is not village:
            self.clear_home_building()
            return None

        return village

    def find_accessible_village(self, world):
        from city import City

        path = self.find_path_to_nearest_tile(
            world,
            lambda tile: (
                isinstance(tile.building, City)
                and tile.building.has_population_space()
            ),
            allow_occupied_target=True,
            max_search_distance=self.VILLAGE_CLAIM_RANGE,
            path_tile_predicate=self.is_habitable_tile,
        )
        if path is None:
            return None

        tile = world.get_tile(self.x, self.y) if not path else world.get_tile(*path[-1])
        if tile is None or not isinstance(tile.building, City):
            return None
        return tile.building

    def can_found_village_on_tile(
        self,
        tile,
        require_farm_site,
        villages,
    ):
        from city import City

        return City.can_place_on_tile(
            tile,
            require_farm_site=require_farm_site,
            villages=villages,
        )

    def initialize_new_village(self, game, village):
        village.try_build_initial_farm(game.world)

    def create_home_village(self, game):
        from city import City

        existing_villages = City.get_villages(game.world)
        require_farm_site = True
        path = None
        for require_farm_site in (True, False):
            path = self.find_path_to_nearest_tile(
                game.world,
                lambda tile, require_farm_site=require_farm_site: self.can_found_village_on_tile(
                    tile,
                    require_farm_site,
                    existing_villages,
                ),
                max_search_distance=self.VILLAGE_CLAIM_RANGE,
                path_tile_predicate=self.is_habitable_tile,
            )
            if path is not None:
                break

        if path is None:
            return None

        tile = game.world.get_tile(self.x, self.y) if not path else game.world.get_tile(*path[-1])
        if not self.can_found_village_on_tile(
            tile,
            require_farm_site,
            existing_villages,
        ):
            return None

        village = City(tile.x, tile.y, level="village", world=game.world)
        tile.building = village
        self.initialize_new_village(game, village)
        return village

    def ensure_home_village(self, game, allow_create=False):
        village = self.get_home_village(game.world)
        if village is not None:
            return village

        village = self.find_accessible_village(game.world)
        if village is None and allow_create:
            village = self.create_home_village(game)

        if village is not None and village.has_population_space():
            self.set_home_building(village)
            village.try_build_initial_farm(game.world)
            return village

        return None

    def handle_successful_meal(self, game, meal_points=None):
        village = self.ensure_home_village(game)
        if village is None:
            # Before founding or joining a village, an ape feeds itself.
            # This lets it earn its first reproduction without prematurely
            # creating a settlement just to store the food.
            if meal_points is None:
                meal_points = 1
            self.meals_eaten += meal_points
            self.set_behavior("eat")
            self.reset_hunger()
            self.heal_from_meal()
            return None

        # Ape prey becomes transportable settlement food. It must be deposited
        # at a connected building before any resident can consume it.
        if meal_points is None:
            meal_points = 1
        self.carrying_food += meal_points
        self.set_behavior("return_food")
        return None

    def is_at_connected_settlement_building(self, world, village):
        tile = world.get_tile(self.x, self.y)
        return (
            tile is not None
            and tile.building is not None
            and village.is_connected_building(world, tile.building)
        )

    def find_path_to_settlement(self, world, village):
        connected_buildings = village.get_connected_buildings(world)
        return self.find_path_to_nearest_tile(
            world,
            lambda tile: tile.building in connected_buildings,
            allow_occupied_target=True,
            path_tile_predicate=self.can_path_through_tile,
        )

    def deposit_carried_food(self, village):
        if not self.carrying_food:
            return False

        village.food += self.carrying_food
        self.carrying_food = 0
        self.clear_settlement_path()
        self.set_behavior("store_food")
        return True

    def consume_village_food(self, game, village):
        if village.food <= 0:
            return False

        village.food -= 1
        self.meals_eaten += 1
        self.reset_hunger()
        self.heal_from_meal()
        self.set_behavior("eat_village_food")
        return True

    def defer_reproduction(self, behavior):
        self.meals_eaten = min(
            self.meals_eaten,
            self.REPRODUCTION_MEAL_THRESHOLD - 1,
        )
        self.set_behavior(behavior)
        return False

    def try_reproduce_in_village(self, game, village):
        if not village.has_population_space():
            village.try_build_residential_district(game.world)

        if not village.has_population_space():
            return self.defer_reproduction("await_housing")

        offspring = self.complete_reproduction(game)
        if offspring is None:
            return self.defer_reproduction("await_birth_space")

        if isinstance(offspring, Ape):
            offspring.set_home_building(village)
        return True

    def arrive_at_settlement(self, game, village):
        self.clear_settlement_path()
        self.deposit_carried_food(village)

        if self.is_hungry and village.food > 0:
            return self.consume_village_food(game, village)

        if self.meals_eaten >= self.REPRODUCTION_MEAL_THRESHOLD:
            return self.try_reproduce_in_village(game, village)

        return True

    def clear_settlement_path(self):
        self.settlement_path = []
        self.settlement_path_village = None

    def try_return_to_settlement(self, game, behavior):
        village = self.ensure_home_village(game)
        if village is None:
            self.clear_settlement_path()
            self.set_behavior("seek_village")
            return False

        if self.is_at_connected_settlement_building(game.world, village):
            return self.arrive_at_settlement(game, village)

        self.set_behavior(behavior)
        path = getattr(self, "settlement_path", [])
        cached_village = getattr(self, "settlement_path_village", None)
        if (
            cached_village is not village
            or not path
            or path[0] not in self.get_neighbor_positions(
                game.world,
                self.x,
                self.y,
            )
        ):
            path = self.find_path_to_settlement(game.world, village)
            self.settlement_path = [] if path is None else path
            self.settlement_path_village = village

        if not path:
            return False

        next_x, next_y = path[0]
        if self.move_to(game.world, next_x, next_y, game):
            self.settlement_path.pop(0)
        else:
            # Occupancy and terrain can change after a route is calculated.
            # Replan on the next movement update instead of every update.
            self.clear_settlement_path()

        if self.is_at_connected_settlement_building(game.world, village):
            self.arrive_at_settlement(game, village)
        return True

    def try_handle_priority_behavior(self, game):
        if self.try_tame_adjacent_wolf(game):
            return True

        village = self.ensure_home_village(game)

        if self.is_hungry and village is not None and village.food > 0:
            return self.consume_village_food(game, village)

        if self.carrying_food:
            return self.try_return_to_settlement(game, "return_food")

        if self.meals_eaten >= self.REPRODUCTION_MEAL_THRESHOLD:
            return self.handle_reproduction_priority(game, village)

        return False

    def handle_reproduction_priority(self, game, village):
        if village is None:
            village = self.ensure_home_village(game, allow_create=True)
            if village is None:
                self.set_behavior("seek_village")
                return False

        if not self.is_at_connected_settlement_building(game.world, village):
            return self.try_return_to_settlement(game, "return_to_reproduce")

        return self.try_reproduce_in_village(game, village)

    def try_wander(self, world, game=None):
        village = self.get_home_village(world)
        if village is None:
            return super().try_wander(world, game)

        self.set_behavior("wander")
        current_distance = self.get_tile_distance(
            world,
            self.x,
            self.y,
            village.x,
            village.y,
        )
        candidates = []
        for dx, dy in CARDINAL_DIRECTIONS:
            nx = (self.x + dx) % world.cols
            ny = self.y + dy
            tile = world.get_tile(nx, ny)
            if not self.can_enter_tile(tile):
                continue

            distance = self.get_tile_distance(
                world,
                nx,
                ny,
                village.x,
                village.y,
            )
            if (
                current_distance <= self.VILLAGE_ROAM_RANGE
                and distance > self.VILLAGE_ROAM_RANGE
            ):
                continue
            candidates.append((distance, nx, ny))

        if not candidates:
            return False

        if current_distance > self.VILLAGE_ROAM_RANGE:
            closest_distance = min(distance for distance, _, _ in candidates)
            candidates = [
                candidate
                for candidate in candidates
                if candidate[0] == closest_distance
            ]

        _, nx, ny = random.choice(candidates)
        return self.move_to(world, nx, ny, game)

    def try_tame_adjacent_wolf(self, game):
        if type(self) is not Ape:
            return False

        village = self.get_home_village(game.world)
        if village is None:
            self.wolf_taming_contact_ids.clear()
            return False

        from .dog import Dog
        from .wolf import Wolf

        adjacent_wolves = []
        for x, y in self.get_neighbor_positions(game.world, self.x, self.y):
            tile = game.world.get_tile(x, y)
            wolf = None if tile is None else tile.critter
            if (
                isinstance(wolf, Wolf)
                and wolf.current_behavior != "dying"
            ):
                adjacent_wolves.append(wolf)

        adjacent_ids = {wolf.id for wolf in adjacent_wolves}
        self.wolf_taming_contact_ids.intersection_update(adjacent_ids)

        for wolf in adjacent_wolves:
            if wolf.id in self.wolf_taming_contact_ids:
                continue

            self.wolf_taming_contact_ids.add(wolf.id)
            if random.random() >= self.WOLF_TAMING_CHANCE:
                continue

            if Dog.tame(wolf, village) is None:
                continue
            self.set_behavior("tame_wolf")
            return True

        return False

    def try_handle_hunter_priority_behavior(self, game):
        """Prioritize assigned prey while preserving carried-food behavior."""
        if self.is_hungry:
            village = self.ensure_home_village(game)
            if village is not None and village.food > 0:
                return self.consume_village_food(game, village)
            if self.carrying_food:
                return Ape.try_handle_priority_behavior(self, game)

        if self.carrying_food:
            if self.should_return_carried_food():
                return Ape.try_handle_priority_behavior(self, game)

            if self.hunt_nearest_prey(
                game,
                self.get_hunt_prey_types(),
                self.get_predator_name(),
            ):
                return True

            # Do not strand a partial haul when no additional prey is in range.
            return Ape.try_handle_priority_behavior(self, game)

        if self.hunt_nearest_prey(
            game,
            self.get_hunt_prey_types(),
            self.get_predator_name(),
        ):
            return True

        return Ape.try_handle_priority_behavior(self, game)

    def should_return_carried_food(self):
        return self.carrying_food > 0

    def take_hungry_action(self, game):
        village = self.ensure_home_village(game)
        if village is not None and self.consume_village_food(game, village):
            return

        if self.hunt_nearest_prey(game, self.get_hunt_prey_types(), self.get_predator_name()):
            return

        self.try_wander(game.world, game)
