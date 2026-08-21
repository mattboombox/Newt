import random

from .critter import Critter, LAND_TERRAINS


class Newt(Critter):
    CRITTER_TAGS = frozenset({"animal", "aquatic", "terrestrial", "vertebrate"})
    ALLOWED_TERRAINS = LAND_TERRAINS | {"lake"}
    FEED_TERRAINS = {"lake", "grass"}
    GRASS_CONSUME_CHANCE = 0.04
    HUNGER_INTERVAL = 24.0
    STARVATION_INTERVAL = 28.0
    MOVE_COOLDOWN = 0.64
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
        return super().try_reproduce(world)

    def take_hungry_action(self, game):
        def graze(tile):
            if tile.terrain == "grass" and random.random() < self.GRASS_CONSUME_CHANCE:
                tile.set_terrain("sand")
            self.handle_successful_meal(game)

        if not self.feed_on_nearest_terrain(
            game,
            Newt.FEED_TERRAINS,
            "seek_food",
            graze,
            require_empty_target=True,
        ):
            self.explore_while_hungry(game)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)
