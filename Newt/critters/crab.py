from .critter import Critter


class Crab(Critter):
    CRITTER_TAGS = frozenset({"animal", "aquatic", "terrestrial", "invertebrate"})
    ALLOWED_TERRAINS = {"beach", "shallows"}
    FEED_TERRAINS = {"shallows"}
    HUNGER_INTERVAL = 14.0
    STARVATION_INTERVAL = 10.0
    REPRODUCTION_MEAL_THRESHOLD = 5
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
        return super().try_reproduce(world)

    def take_hungry_action(self, game):
        if not self.feed_on_nearest_terrain(
            game,
            Crab.FEED_TERRAINS,
            "seek_shallows",
        ):
            self.explore_while_hungry(game)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_meal_based_plankton_remains(game, tile)

    def get_reproduction_blocking_types(self):
        return (Crab,)
