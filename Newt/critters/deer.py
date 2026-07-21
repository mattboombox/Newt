import random

from .crab import Crab
from .critter import Critter, NON_ARCTIC_LAND_TERRAINS


class Deer(Critter):
    ALLOWED_TERRAINS = NON_ARCTIC_LAND_TERRAINS
    HUNGER_INTERVAL = 40.0
    STARVATION_INTERVAL = 40.0
    GRASS_CONSUME_CHANCE = 0.10
    FLEE_DETECTION_RADIUS = 5
    DISPLACEABLE_CRITTER_TYPES = (Crab,)

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(180, 140, 90),
            allowed_terrains=Deer.ALLOWED_TERRAINS,
            move_cooldown=0.18,
            sprite="deer"
        )
        self.configure_hunger(Deer.HUNGER_INTERVAL, Deer.STARVATION_INTERVAL)

    def get_flee_predator_types(self):
        from .wolf import Wolf

        return (Wolf,)

    def take_hungry_action(self, game):
        def eat_grass(tile):
            if random.random() < Deer.GRASS_CONSUME_CHANCE:
                tile.set_terrain("sand")
            self.handle_successful_meal(game)

        self.feed_on_nearest_terrain(game, {"grass"}, "seek_grass", eat_grass, require_empty_target=True)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)
