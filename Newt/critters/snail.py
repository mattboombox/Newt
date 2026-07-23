from .critter import AMPHIBIOUS_LAND_TERRAINS, Critter


class Snail(Critter):
    """A lake-feeding mollusk descended from nautilus in shallow water."""

    ALLOWED_TERRAINS = AMPHIBIOUS_LAND_TERRAINS - {"shallows"}
    FEED_TERRAINS = {"lake"}
    HUNGER_INTERVAL = 28.0
    STARVATION_INTERVAL = 32.0
    MOVE_COOLDOWN = 0.56
    REPRODUCTION_MEAL_THRESHOLD = 3

    def __init__(self, x, y):
        super().__init__(
            x,
            y,
            color=(180, 150, 115),
            allowed_terrains=Snail.ALLOWED_TERRAINS,
            move_cooldown=Snail.MOVE_COOLDOWN,
            sprite="snail",
        )
        self.configure_hunger(Snail.HUNGER_INTERVAL, Snail.STARVATION_INTERVAL)

    def try_reproduce(self, world):
        current_tile = world.get_tile(self.x, self.y)
        if current_tile is None or current_tile.terrain not in Snail.FEED_TERRAINS:
            return self.fail_reproduction_attempt(reset_meals=True)

        offspring = self.try_spawn_adjacent_offspring(world, self.is_habitable_tile)
        if offspring is not None:
            return offspring

        return self.fail_reproduction_attempt(reset_meals=True)

    def take_hungry_action(self, game):
        self.feed_on_nearest_terrain(game, Snail.FEED_TERRAINS, "seek_lake", require_empty_target=True)
