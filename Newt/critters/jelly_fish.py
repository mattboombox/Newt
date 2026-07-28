from .critter import AQUATIC_TERRAINS, Critter
from .plankton import Plankton


class Jellyfish(Critter):
    """A passive drifting predator that feeds only through collisions."""

    DISPLACEMENT_LEVEL = 2
    ALLOWED_TERRAINS = AQUATIC_TERRAINS - {"lake"}
    REPRODUCTION_MEAL_THRESHOLD = 8
    HUNGER_INTERVAL = 120.0
    STARVATION_INTERVAL = 240.0
    MOVE_COOLDOWN = Plankton.MOVE_COOLDOWN
    PREDATOR_NAME = "Jellyfish"

    def __init__(self, x, y):
        super().__init__(
            x,
            y,
            color=(180, 160, 240),
            allowed_terrains=Jellyfish.ALLOWED_TERRAINS,
            move_cooldown=Jellyfish.MOVE_COOLDOWN,
            sprite="jelly_fish",
        )
        self.configure_hunger(
            Jellyfish.HUNGER_INTERVAL,
            Jellyfish.STARVATION_INTERVAL,
        )

    @staticmethod
    def is_collision_prey(critter):
        from .squid_egg import SquidEgg

        if isinstance(critter, SquidEgg):
            return False

        allowed_terrains = getattr(critter, "allowed_terrains", set()) or set()
        is_ocean_dweller = bool({"ocean", "trench"} & set(allowed_terrains))
        return (
            is_ocean_dweller
            and critter.DISPLACEMENT_LEVEL in {0, 1}
        )

    def can_displace_critter(self, critter):
        return self.is_collision_prey(critter)

    def should_attempt_shove_displacement(self, critter):
        return False

    def get_displacement_meal_value(self, critter):
        if self.is_collision_prey(critter):
            return self.get_reproduction_meal_value(critter)
        return None

    def take_hungry_action(self, game):
        self.try_wander(game.world, game)

    def get_reproduction_blocking_types(self):
        return (Jellyfish,)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_meal_based_plankton_remains(game, tile)
