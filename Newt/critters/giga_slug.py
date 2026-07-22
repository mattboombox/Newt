import random

from .critter import Critter, NON_ARCTIC_LAND_TERRAINS


class GigaSlug(Critter):
    """A slow, large grazing descendant of the shoreline snail."""

    ALLOWED_TERRAINS = NON_ARCTIC_LAND_TERRAINS | {"shallows"}
    HUNGER_INTERVAL = 40.0
    STARVATION_INTERVAL = 40.0
    GRASS_CONSUME_CHANCE = 0.10
    FLEE_DETECTION_RADIUS = 5
    MOVE_COOLDOWN = 0.72

    def __init__(self, x, y):
        super().__init__(
            x,
            y,
            color=(120, 105, 85),
            allowed_terrains=GigaSlug.ALLOWED_TERRAINS,
            move_cooldown=GigaSlug.MOVE_COOLDOWN,
            sprite="giga_slug",
        )
        self.configure_hunger(GigaSlug.HUNGER_INTERVAL, GigaSlug.STARVATION_INTERVAL)

    def get_flee_predator_types(self):
        from .land_kraken import LandKraken

        return (LandKraken,)

    def take_hungry_action(self, game):
        def eat_grass(tile):
            if random.random() < GigaSlug.GRASS_CONSUME_CHANCE:
                tile.set_terrain("sand")
            self.handle_successful_meal(game)

        self.feed_on_nearest_terrain(game, {"grass"}, "seek_grass", eat_grass, require_empty_target=True)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)
