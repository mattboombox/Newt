from .critter import Critter
from .wolf import Wolf


class Herrera(Critter):
    """A wolf-like combat predator without den behavior."""

    COMBAT_CAPABLE = Wolf.COMBAT_CAPABLE
    COMBAT_POWER = Wolf.COMBAT_POWER
    MAX_COMBAT_HEALTH = Wolf.MAX_COMBAT_HEALTH
    DISPLACEMENT_LEVEL = Wolf.DISPLACEMENT_LEVEL
    ALLOWED_TERRAINS = Wolf.ALLOWED_TERRAINS
    REPRODUCTION_MEAL_THRESHOLD = Wolf.REPRODUCTION_MEAL_THRESHOLD
    HUNGER_INTERVAL = Wolf.HUNGER_INTERVAL
    STARVATION_INTERVAL = Wolf.STARVATION_INTERVAL
    HUNT_RANGE = Wolf.HUNT_RANGE
    SCAVENGE_PREY_TYPES = Wolf.SCAVENGE_PREY_TYPES
    PREDATOR_NAME = "Herrera"
    REPRODUCTION_BLOCKS_SET_BEHAVIOR = True
    REPRODUCTION_BLOCKS_RESET_MEALS = True

    def __init__(self, x, y):
        super().__init__(
            x,
            y,
            color=(155, 125, 85),
            allowed_terrains=self.ALLOWED_TERRAINS,
            move_cooldown=0.32,
            sprite="herrera",
        )
        self.configure_hunger(self.HUNGER_INTERVAL, self.STARVATION_INTERVAL)

    def get_hunt_prey_types(self):
        return Wolf.HUNT_PREY_TYPES + (Wolf,)

    def take_hungry_action(self, game):
        if self.hunt_nearest_prey(
            game,
            self.get_hunt_prey_types(),
            self.get_predator_name(),
        ):
            return

        self.try_wander(game.world, game)

    def get_reproduction_blocking_types(self):
        return (Herrera,)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)

