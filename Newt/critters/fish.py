from .crab import Crab
from .critter import Critter
from .plankton import Plankton


class Fish(Critter):
    ALLOWED_TERRAINS = {"ocean", "shallows", "lake"}
    REPRODUCTION_MEAL_THRESHOLD = 2
    HUNGER_INTERVAL = 40.0
    STARVATION_INTERVAL = 40.0
    FLEE_DETECTION_RADIUS = 5
    # Keep a hungry school from searching the entire large map every time it
    # moves.  Food farther away is reconsidered on the next decision tick.
    HUNT_RANGE = 12
    SCAVENGE_RANGE = 12
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
        from .sea_scorpion import SeaScorpion

        return (Squid, SeaScorpion)

    def get_hunt_prey_types(self):

        return (Plankton, Crab,)

    def get_scavenge_prey_types(self):
        return self.get_hunt_prey_types()

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_plankton_remains(game, tile)
