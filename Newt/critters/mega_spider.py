from .critter import Critter, NON_ARCTIC_LAND_TERRAINS


class MegaSpider(Critter):
    ALLOWED_TERRAINS = NON_ARCTIC_LAND_TERRAINS
    REPRODUCTION_MEAL_THRESHOLD = 8
    HUNGER_INTERVAL = 70.0
    STARVATION_INTERVAL = 120.0
    HUNT_RANGE = 18
    PREDATOR_NAME = "Mega Spider"
    WEB_CLAIM_RANGE = 28

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(65, 55, 75),
            allowed_terrains=MegaSpider.ALLOWED_TERRAINS,
            move_cooldown=0.48,
            sprite="mega_spider",
        )
        self.configure_hunger(MegaSpider.HUNGER_INTERVAL, MegaSpider.STARVATION_INTERVAL)
        self.carrying_web_seed = False

    def get_home_web(self, world):
        from building import SpiderWeb

        web = self.home_building
        if not isinstance(web, SpiderWeb):
            return None

        tile = world.get_tile(web.x, web.y)
        if tile is None or tile.building is not web or not SpiderWeb.can_place_on_tile(tile):
            self.clear_home_building()
            return None

        return web

    def get_hunt_prey_types(self):
        from . import CRITTER_TYPES, SquidEgg

        return tuple(CRITTER_TYPES.values()) + (SquidEgg,)

    def get_scavenge_prey_types(self):
        return self.get_hunt_prey_types()

    def create_home_web(self, game):
        from building import SpiderWeb

        path = self.find_path_to_nearest_tile(
            game.world,
            lambda tile: tile.building is None and SpiderWeb.can_place_on_tile(tile),
            max_search_distance=MegaSpider.WEB_CLAIM_RANGE,
            path_tile_predicate=self.is_habitable_tile,
        )
        if path is None:
            return None

        tile = game.world.get_tile(self.x, self.y) if not path else game.world.get_tile(*path[-1])
        if tile is None or tile.building is not None or not SpiderWeb.can_place_on_tile(tile):
            return None

        web = SpiderWeb(tile.x, tile.y, world=game.world)
        tile.building = web
        return web

    def ensure_home_web(self, game):
        web = self.get_home_web(game.world)
        if web is not None:
            return web

        web = self.create_home_web(game)
        if web is not None:
            self.set_home_building(web)
        return web

    def deposit_web_seed(self):
        self.carrying_web_seed = False
        self.set_behavior("reproduce")
        return True

    def consume_web_charge(self, world):
        web = self.get_home_web(world)
        if web is None or web.charges <= 0:
            return False

        web.charges -= 1
        self.reset_hunger()
        self.set_behavior("eat_web_reserve")
        return True

    def update_hunger(self, game, dt):
        was_hungry = self.is_hungry
        can_continue = super().update_hunger(game, dt)
        if can_continue and not was_hungry and self.is_hungry:
            self.consume_web_charge(game.world)
        return can_continue

    def handle_successful_meal(self, game, meal_points=None):
        if meal_points is None:
            meal_points = 1

        self.meals_eaten += meal_points
        self.set_behavior("eat")
        self.reset_hunger()

        if self.meals_eaten < self.REPRODUCTION_MEAL_THRESHOLD:
            return None

        if self.get_home_web(game.world) is None:
            self.carrying_web_seed = True
            self.meals_eaten = 0
            self.set_behavior("return_web")
            return None

        offspring = self.try_reproduce(game.world)
        if offspring is not None:
            game.critters.append(offspring)
            self.meals_eaten = 0
        return offspring

    def try_return_to_web(self, game):
        web = self.ensure_home_web(game)
        if web is None:
            self.set_behavior("return_web")
            return False

        if self.x == web.x and self.y == web.y:
            if web.has_trapped_prey(game.world):
                return web.consume_trapped_prey(game, self)
            return self.deposit_web_seed() if self.carrying_web_seed else True

        path = self.find_path_to_nearest_tile(
            game.world,
            lambda tile: tile.x == web.x and tile.y == web.y,
            allow_occupied_target=True,
            path_tile_predicate=self.is_habitable_tile,
        )
        if not path:
            self.set_behavior("return_web")
            return False

        next_x, next_y = path[0]
        if next_x == web.x and next_y == web.y and web.has_trapped_prey(game.world):
            web.consume_trapped_prey(game, self)

        self.set_behavior("return_web")
        return self.move_to(game.world, next_x, next_y, game)

    def try_handle_priority_behavior(self, game):
        web = self.get_home_web(game.world)
        if self.carrying_web_seed or (
            web is not None
            and (
                web.has_trapped_prey(game.world)
                or (self.is_hungry and web.charges > 0)
            )
        ):
            return self.try_return_to_web(game)
        return False

    def take_hungry_action(self, game):
        web = self.get_home_web(game.world)
        if web is not None and web.charges > 0:
            self.set_behavior("return_web")
            return

        if self.hunt_nearest_prey(game, self.get_hunt_prey_types(), self.get_predator_name()):
            return

        self.try_wander(game.world, game)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)
