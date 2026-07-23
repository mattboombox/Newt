from .critter import Critter
from .plankton import Plankton


class Nautilus(Critter):
    ALLOWED_TERRAINS = {"ocean", "trench", "shallows"}
    REPRODUCTION_MEAL_THRESHOLD = 5
    HUNGER_INTERVAL = 55.0
    STARVATION_INTERVAL = 55.0
    HUNT_RANGE = 12
    HUNT_PREY_TYPES = (Plankton,)
    DISPLACEABLE_CRITTER_TYPES = (Plankton,)
    PREDATOR_NAME = "Nautilus"

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(205, 185, 140),
            allowed_terrains=Nautilus.ALLOWED_TERRAINS,
            move_cooldown=0.42,
            sprite="nautilus"
        )
        self.configure_hunger(Nautilus.HUNGER_INTERVAL, Nautilus.STARVATION_INTERVAL)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_plankton_remains(game, tile)
