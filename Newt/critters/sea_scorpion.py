from .critter import AQUATIC_TERRAINS, Critter
from .crab import Crab
from .fish import Fish
from .nautilus import Nautilus
from .trilobite import Trilobite


class SeaScorpion(Critter):
    """A swift coastal predator descended from the trilobite branch."""

    CRITTER_TAGS = frozenset({"animal", "aquatic", "invertebrate", "predator"})
    HUNT_PREY_RULE = (Fish, Nautilus, Trilobite, Crab)
    SCAVENGE_PREY_RULE = HUNT_PREY_RULE
    BODY_SIZE = 2
    ALLOWED_TERRAINS = AQUATIC_TERRAINS - {"lake", "trench"}
    REPRODUCTION_MEAL_THRESHOLD = 5
    HUNGER_INTERVAL = 110.0
    STARVATION_INTERVAL = 120.0
    HUNT_RANGE = 4
    PREDATOR_NAME = "Sea Scorpion"

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(145, 95, 75),
            allowed_terrains=SeaScorpion.ALLOWED_TERRAINS,
            move_cooldown=0.70,
            sprite="sea_scorpion",
        )
        self.configure_hunger(SeaScorpion.HUNGER_INTERVAL, SeaScorpion.STARVATION_INTERVAL)

    def try_reproduce(self, world):
        current_tile = world.get_tile(self.x, self.y)
        if current_tile is None or current_tile.terrain != "shallows":
            return self.fail_reproduction_attempt(reset_meals=True)

        return super().try_reproduce(world)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_meal_based_plankton_remains(game, tile)
