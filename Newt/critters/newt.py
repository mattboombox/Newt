from .crab import Crab
from .critter import AMPHIBIOUS_LAND_TERRAINS, Critter


class Newt(Critter):
    ALLOWED_TERRAINS = AMPHIBIOUS_LAND_TERRAINS - {"shallows"}
    FEED_TERRAINS = {"lake"}
    HUNGER_INTERVAL = 24.0
    STARVATION_INTERVAL = 28.0
    MOVE_COOLDOWN = 0.64
    DISPLACEABLE_CRITTER_TYPES = (Crab,)
    REPRODUCTION_MEAL_THRESHOLD = 3

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(110, 200, 120),
            allowed_terrains=Newt.ALLOWED_TERRAINS,
            move_cooldown=Newt.MOVE_COOLDOWN,
            sprite="newt"
        )
        self.configure_hunger(Newt.HUNGER_INTERVAL, Newt.STARVATION_INTERVAL)

    def try_reproduce(self, world):
        current_tile = world.get_tile(self.x, self.y)
        if current_tile is None or current_tile.terrain not in Newt.FEED_TERRAINS:
            return self.fail_reproduction_attempt(reset_meals=True)

        offspring = self.try_spawn_adjacent_offspring(world, self.is_habitable_tile)
        if offspring is not None:
            return offspring

        return self.fail_reproduction_attempt(reset_meals=True)

    def take_hungry_action(self, game):
        self.feed_on_nearest_terrain(game, Newt.FEED_TERRAINS, "seek_lake", require_empty_target=True)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)
