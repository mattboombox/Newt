from .critter import AQUATIC_TERRAINS, Critter, PreyRule
from .crab import Crab
from .fish import Fish
from .nautilus import Nautilus
from .trilobite import Trilobite


class Squid(Critter):
    CRITTER_TAGS = frozenset({"animal", "aquatic", "invertebrate", "predator"})
    BODY_SIZE = 2
    ALLOWED_TERRAINS = AQUATIC_TERRAINS
    REPRODUCTION_MEAL_THRESHOLD = 5
    HUNGER_INTERVAL = 200.0
    STARVATION_INTERVAL = 300.0
    # Fish move every 0.18 seconds. Squid need a clear pursuit advantage or
    # fleeing fish can keep them at range until they starve.
    MOVE_COOLDOWN = 0.12
    HUNT_RANGE = 8
    HUNT_PREY_RULE = (Fish, Nautilus, Trilobite, Crab)
    SCAVENGE_PREY_RULE = HUNT_PREY_RULE
    PRIORITY_PREY_TYPES = (Fish,)
    PREDATOR_NAME = "Squid"
    REPRODUCTION_BLOCKS_SET_BEHAVIOR = True
    REPRODUCTION_BLOCKS_RESET_MEALS = True

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(180, 120, 220),
            allowed_terrains=Squid.ALLOWED_TERRAINS,
            move_cooldown=Squid.MOVE_COOLDOWN,
            sprite="squid"
        )
        self.configure_hunger(Squid.HUNGER_INTERVAL, Squid.STARVATION_INTERVAL)

    def create_offspring(self, x, y):
        from .squid_egg import SquidEgg

        return SquidEgg(x, y)

    def get_scavenge_range(self):
        return self.get_hunt_range()

    def get_reproduction_blocking_types(self):
        return (Squid,)

    def spawn_death_remains(self, game, tile):
        if self.try_spawn_meal_based_remains(
            lambda: self.try_spawn_squid_egg_remains(game, tile),
        ):
            return True

        return self.try_spawn_meal_based_plankton_remains(game, tile)
