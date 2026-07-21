from .crab import Crab
from .critter import Critter
from .plankton import Plankton


class Fish(Critter):
    ALLOWED_TERRAINS = {"ocean", "shallows", "lake"}
    REPRODUCTION_MEAL_THRESHOLD = 3
    HUNGER_INTERVAL = 40.0
    STARVATION_INTERVAL = 40.0
    FLEE_DETECTION_RADIUS = 5
    HUNT_PREY_TYPES = (Plankton)
    SCAVENGE_PREY_TYPES = (Plankton, Crab)
    DISPLACEABLE_CRITTER_TYPES = (Plankton, Crab)
    PREDATOR_NAME = "Fish"

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(80, 180, 255),
            allowed_terrains=Fish.ALLOWED_TERRAINS,
            move_cooldown=0.18,
            sprite="fish"
        )
        self.configure_hunger(Fish.HUNGER_INTERVAL, Fish.STARVATION_INTERVAL)

    def get_flee_predator_types(self):
        from .squid import Squid

        return (Squid,)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_plankton_remains(game, tile)
