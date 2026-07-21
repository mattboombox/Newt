from .critter import Critter


class Crab(Critter):
    ALLOWED_TERRAINS = {"beach", "shallows"}
    FEED_TERRAINS = {"shallows"}
    HUNGER_INTERVAL = 14.0
    STARVATION_INTERVAL = 10.0
    DISPLACEABLE_CRITTER_TYPES = ()
    REPRODUCTION_BLOCKS_SET_BEHAVIOR = True
    REPRODUCTION_BLOCKS_RESET_MEALS = True

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(255, 80, 80),
            allowed_terrains=Crab.ALLOWED_TERRAINS,
            move_cooldown=0.30,
            sprite="crab"
        )
        self.configure_hunger(Crab.HUNGER_INTERVAL, Crab.STARVATION_INTERVAL)

    def try_reproduce(self, world):
        current_tile = world.get_tile(self.x, self.y)
        if current_tile is None or current_tile.terrain not in Crab.FEED_TERRAINS:
            return self.fail_reproduction_attempt(reset_meals=True)

        if self.is_reproduction_blocked(world):
            self.handle_blocked_reproduction()
            return None

        offspring = self.try_spawn_adjacent_offspring(
            world,
            lambda tile: tile.terrain in Crab.FEED_TERRAINS,
        )
        if offspring is not None:
            return offspring

        return self.fail_reproduction_attempt(reset_meals=True)

    def take_hungry_action(self, game):
        self.feed_on_nearest_terrain(game, Crab.FEED_TERRAINS, "seek_shallows")

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_plankton_remains(game, tile)

    def get_reproduction_blocking_types(self):
        return (Crab,)
