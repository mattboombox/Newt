from .crab import Crab
from .critter import AQUATIC_TERRAINS, Critter
from .fish import Fish
from .nautilus import Nautilus
from .trilobite import Trilobite


class Squid(Critter):
    DISPLACEMENT_LEVEL = 2
    ALLOWED_TERRAINS = AQUATIC_TERRAINS
    REPRODUCTION_MEAL_THRESHOLD = 10
    HUNGER_INTERVAL = 200.0
    STARVATION_INTERVAL = 120.0
    MOVE_COOLDOWN = 0.48
    HUNT_RANGE = 8
    HUNT_PREY_TYPES = (Fish, Crab, Nautilus, Trilobite)
    SCAVENGE_PREY_TYPES = (Fish, Crab, Nautilus, Trilobite)
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

    def get_scavenge_prey_types(self):
        return super().get_scavenge_prey_types() + (Squid,)

    def get_scavenge_range(self):
        return self.get_hunt_range()

    def take_hungry_action(self, game):
        if self.hunt_nearest_prey(game, (Fish, Nautilus, Trilobite), self.get_predator_name()):
            return

        self.try_wander(game.world, game)

    def get_reproduction_blocking_types(self):
        return (Squid,)

    def spawn_death_remains(self, game, tile):
        if self.try_spawn_meal_based_remains(
            lambda: self.try_spawn_squid_egg_remains(game, tile),
        ):
            return True

        return self.try_spawn_meal_based_plankton_remains(game, tile)
