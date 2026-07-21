from .crab import Crab
from .critter import Critter
from .plankton import Plankton


class Whale(Critter):
    ALLOWED_TERRAINS = {"ocean", "trench", "shallows"}
    REPRODUCTION_MEAL_THRESHOLD = 15
    HUNGER_INTERVAL = 18.0
    STARVATION_INTERVAL = 90.0
    HUNT_PREY_TYPES = (Plankton,)
    DISPLACEABLE_CRITTER_TYPES = (Plankton, Crab)
    DISPLACEMENT_MEAL_TYPES = (Plankton,)
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
        return self.try_spawn_plankton_remains(game, tile)
