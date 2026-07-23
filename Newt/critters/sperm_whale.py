from .crab import Crab
from .plankton import Plankton
from .whale import Whale
from .trilobite import Trilobite


class SpermWhale(Whale):
    ALLOWED_TERRAINS = Whale.ALLOWED_TERRAINS
    REPRODUCTION_MEAL_THRESHOLD = Whale.REPRODUCTION_MEAL_THRESHOLD
    HUNGER_INTERVAL = Whale.HUNGER_INTERVAL
    STARVATION_INTERVAL = 220.0
    DISPLACEABLE_CRITTER_TYPES = (Plankton, Crab, Trilobite)
    PREDATOR_NAME = "Sperm Whale"

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = (95, 105, 125)
        self.allowed_terrains = SpermWhale.ALLOWED_TERRAINS
        self.move_cooldown = 0.36
        self.sprite = "sperm_whale"
        self.configure_hunger(SpermWhale.HUNGER_INTERVAL, SpermWhale.STARVATION_INTERVAL)

    def get_hunt_prey_types(self):
        from .land_kraken import LandKraken
        from .squid import Squid
        from .sea_scorpion import SeaScorpion

        return (Squid, LandKraken, SeaScorpion)

    def get_scavenge_prey_types(self):
        return self.get_hunt_prey_types()

    def try_scavenge_corpse(self, game):
        # A hungry whale must use its sonar to hunt live prey instead of
        # committing to a potentially distant corpse.  Nearby corpses are
        # still consumed by the base class's quick adjacent check.
        if self.is_hungry:
            return False

        return super().try_scavenge_corpse(game)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_plankton_remains(game, tile)
