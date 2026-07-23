from .crab import Crab
from .critter import Critter, NON_ARCTIC_LAND_TERRAINS
from .deer import Deer
from .therapsid import Therapsid


class Wolf(Critter):
    ALLOWED_TERRAINS = NON_ARCTIC_LAND_TERRAINS
    REPRODUCTION_MEAL_THRESHOLD = 5
    HUNGER_INTERVAL = 260.0
    STARVATION_INTERVAL = 120.0
    HUNT_RANGE = 8
    HUNT_PREY_TYPES = (Deer, Therapsid)
    SCAVENGE_PREY_TYPES = (Deer, Therapsid)
    DISPLACEABLE_CRITTER_TYPES = (Crab,)
    PREDATOR_NAME = "Wolf"
    REPRODUCTION_BLOCKS_SET_BEHAVIOR = True
    REPRODUCTION_BLOCKS_RESET_MEALS = True
    DEN_CLAIM_RANGE = 28

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(160, 160, 160),
            allowed_terrains=Wolf.ALLOWED_TERRAINS,
            move_cooldown=0.32,
            sprite="wolf"
        )
        self.configure_hunger(Wolf.HUNGER_INTERVAL, Wolf.STARVATION_INTERVAL)
        self.carrying_den_charge = False

    def handle_successful_meal(self, game, meal_points=None):
        if meal_points is None:
            meal_points = 1

        self.meals_eaten += meal_points
        self.set_behavior("eat")
        self.reset_hunger()

        if self.meals_eaten < self.REPRODUCTION_MEAL_THRESHOLD or self.carrying_den_charge:
            return None

        self.carrying_den_charge = True
        self.meals_eaten = 0
        self.set_behavior("return_home")
        return None

    def try_handle_priority_behavior(self, game):
        if not self.carrying_den_charge:
            return False

        return self.try_return_to_den(game)

    def is_returning_home(self):
        return self.carrying_den_charge or self.current_behavior == "return_home"

    def can_displace_critter(self, critter):
        if self.is_returning_home():
            return True

        return super().can_displace_critter(critter)

    def should_remove_on_failed_displacement(self, critter):
        if self.is_returning_home():
            return True

        if isinstance(critter, Deer):
            return False

        return super().should_remove_on_failed_displacement(critter)

    def can_path_through_tile(self, tile):
        if (
            self.is_returning_home()
            and tile is not None
            and tile.critter is not None
            and self.is_habitable_tile(tile)
        ):
            return True

        return super().can_path_through_tile(tile)

    def take_hungry_action(self, game):
        if self.hunt_nearest_prey(game, self.get_hunt_prey_types(), self.get_predator_name()):
            return

        self.try_wander(game.world, game)

    def find_accessible_home_den(self, world, max_search_distance=None):
        from building import WolfDen

        path = self.find_path_to_nearest_tile(
            world,
            lambda tile: isinstance(tile.building, WolfDen),
            max_search_distance=max_search_distance,
            path_tile_predicate=self.is_habitable_tile,
        )
        if path is None:
            return None

        if not path:
            tile = world.get_tile(self.x, self.y)
        else:
            tile = world.get_tile(path[-1][0], path[-1][1])

        if tile is None or not isinstance(tile.building, WolfDen):
            return None

        return tile.building

    def create_home_den(self, game):
        from building import WolfDen

        path = self.find_path_to_nearest_tile(
            game.world,
            lambda tile: tile.building is None and WolfDen.can_place_on_tile(tile),
            path_tile_predicate=self.is_habitable_tile,
        )
        if path is None:
            return None

        if not path:
            tile = game.world.get_tile(self.x, self.y)
        else:
            tile = game.world.get_tile(path[-1][0], path[-1][1])

        if tile is None or tile.building is not None or not WolfDen.can_place_on_tile(tile):
            return None

        den = WolfDen(tile.x, tile.y)
        tile.building = den
        return den

    def ensure_home_den(self, game):
        from building import WolfDen

        if isinstance(self.home_building, WolfDen):
            home_tile = game.world.get_tile(self.home_building.x, self.home_building.y)
            if (
                home_tile is not None
                and home_tile.building is self.home_building
                and WolfDen.can_place_on_tile(home_tile)
            ):
                return self.home_building
            self.clear_home_building()

        den = self.find_accessible_home_den(game.world, max_search_distance=Wolf.DEN_CLAIM_RANGE)
        if den is None:
            den = self.create_home_den(game)

        if den is not None:
            self.set_home_building(den)

        return den

    def deposit_den_charge(self):
        if self.home_building is None:
            return False

        self.home_building.charges += 1
        self.carrying_den_charge = False
        self.set_behavior("reproduce")
        return True

    def try_return_to_den(self, game):
        den = self.ensure_home_den(game)
        if den is None:
            self.set_behavior("return_home")
            return False

        if self.x == den.x and self.y == den.y:
            return self.deposit_den_charge()

        path = self.find_path_to_nearest_tile(
            game.world,
            lambda tile: tile.x == den.x and tile.y == den.y,
            path_tile_predicate=self.is_habitable_tile,
        )
        if path is None:
            self.clear_home_building()
            den = self.create_home_den(game)
            if den is None:
                self.set_behavior("return_home")
                return False
            self.set_home_building(den)
            return self.deposit_den_charge()

        self.set_behavior("return_home")
        if not path:
            return self.deposit_den_charge()

        next_x, next_y = path[0]
        self.move_to(game.world, next_x, next_y, game)
        return True

    def get_reproduction_blocking_types(self):
        return (Wolf,)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)
