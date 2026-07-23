from .critter import Critter
from .plankton import Plankton



class Trilobite(Critter):
    """An early seafloor arthropod that survives by scavenging carrion."""

    ALLOWED_TERRAINS = {"ocean", "trench", "shallows"}
    FEED_TERRAINS = {"shallows"}
    REPRODUCTION_MEAL_THRESHOLD = 3
    HUNGER_INTERVAL = 50.0
    STARVATION_INTERVAL = 45.0
    SCAVENGE_RANGE = 14
    FORAGE_RANGE = 10
    DISPLACEABLE_CRITTER_TYPES = (Plankton,)
    PREDATOR_NAME = "Trilobite"
    REPRODUCTION_BLOCKS_SET_BEHAVIOR = True
    REPRODUCTION_BLOCKS_RESET_MEALS = True

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(110, 125, 145),
            allowed_terrains=Trilobite.ALLOWED_TERRAINS,
            move_cooldown=0.50,
            sprite="trilobite",
        )
        self.configure_hunger(Trilobite.HUNGER_INTERVAL, Trilobite.STARVATION_INTERVAL)

    def take_hungry_action(self, game):
        self.feed_on_nearest_terrain(
            game,
            Trilobite.FEED_TERRAINS,
            "seek_detritus",
        )

    def get_scavenge_prey_types(self):
        # Deferred imports avoid the Squid → Trilobite module cycle at startup.
        from . import CRITTER_TYPES, SquidEgg

        return tuple(CRITTER_TYPES.values()) + (SquidEgg,)

    def get_reproduction_blocking_types(self):
        return (Trilobite,)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_plankton_remains(game, tile)
