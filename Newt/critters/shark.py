from .critter import AQUATIC_TERRAINS, Critter
from .crab import Crab
from .fish import Fish
from .nautilus import Nautilus
from .trilobite import Trilobite


class Shark(Critter):
    """A fast aquatic predator evolved from fish."""

    CRITTER_TAGS = frozenset({"animal", "aquatic", "vertebrate", "predator"})
    BODY_SIZE = 2
    ALLOWED_TERRAINS = AQUATIC_TERRAINS
    REPRODUCTION_MEAL_THRESHOLD = 5
    HUNGER_INTERVAL = 200.0
    STARVATION_INTERVAL = 300.0
    MOVE_COOLDOWN = 0.12
    HUNT_RANGE = 8
    HUNT_PREY_RULE = (Fish, Nautilus, Trilobite, Crab)
    SCAVENGE_PREY_RULE = HUNT_PREY_RULE
    PRIORITY_PREY_TYPES = (Fish,)
    PREDATOR_NAME = "Shark"
    REPRODUCTION_BLOCKS_SET_BEHAVIOR = True
    REPRODUCTION_BLOCKS_RESET_MEALS = True

    def __init__(self, x, y):
        super().__init__(
            x,
            y,
            color=(70, 110, 145),
            allowed_terrains=Shark.ALLOWED_TERRAINS,
            move_cooldown=Shark.MOVE_COOLDOWN,
            sprite="shark",
        )
        self.configure_hunger(Shark.HUNGER_INTERVAL, Shark.STARVATION_INTERVAL)

    def get_scavenge_range(self):
        return self.get_hunt_range()

    def get_reproduction_blocking_types(self):
        return (Shark,)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_meal_based_plankton_remains(game, tile)
