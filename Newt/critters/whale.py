from .critter import AQUATIC_TERRAINS, Critter
from .plankton import Plankton
from .plankton import Plankton


class Whale(Critter):
    CRITTER_TAGS = frozenset({"animal", "aquatic", "vertebrate", "filter_feeder"})
    BODY_SIZE = 3
    ALLOWED_TERRAINS = AQUATIC_TERRAINS - {"lake"}
    REPRODUCTION_MEAL_THRESHOLD = 15
    HUNGER_INTERVAL = 18.0
    STARVATION_INTERVAL = 90.0
    # Sonar gives whales a much longer—but still finite—prey search range.
    HUNT_RANGE = 24
    HUNT_PREY_TYPES = (Plankton,)
    PRIORITY_PREY_TYPES = (Plankton,)
    PREDATOR_NAME = "Whale"

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(110, 150, 190),
            allowed_terrains=Whale.ALLOWED_TERRAINS,
            move_cooldown=0.36,
            sprite="whale"
        )
        self.configure_hunger(Whale.HUNGER_INTERVAL, Whale.STARVATION_INTERVAL)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_meal_based_plankton_remains(game, tile)

    def get_displacement_meal_value(self, critter):
        if isinstance(critter, Plankton):
            return self.get_reproduction_meal_value(critter)
        return None
