import random

from .crab import Crab
from .critter import Critter
from .fish import Fish
from .nautilus import Nautilus
from .newt import Newt
from .plankton import Plankton
from .snail import Snail
from .trilobite import Trilobite


class Squid(Critter):
    ALLOWED_TERRAINS = {"ocean", "trench", "shallows", "lake"}
    REPRODUCTION_MEAL_THRESHOLD = 6
    HUNGER_INTERVAL = 200.0
    STARVATION_INTERVAL = 120.0
    MOVE_COOLDOWN = 0.48
    HUNT_RANGE = 8
    HUNT_PREY_TYPES = (Fish, Crab, Nautilus, Trilobite)
    SCAVENGE_PREY_TYPES = (Fish, Crab, Nautilus, Trilobite)
    DISPLACEABLE_CRITTER_TYPES = (Plankton, Crab, Trilobite)
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
        egg_spawn_chance = min(1.0, self.meals_eaten / Squid.REPRODUCTION_MEAL_THRESHOLD)
        if self.meals_eaten > 0 and random.random() < egg_spawn_chance and self.try_spawn_squid_egg_remains(game, tile):
            return True

        return self.try_spawn_plankton_remains(game, tile)
