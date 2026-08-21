from .ape import Ape
from .critter import Critter, LAND_TERRAINS
from .giga_slug import GigaSlug
from .therapsid import Therapsid


class LandKraken(Critter):
    CRITTER_TAGS = frozenset({"animal", "terrestrial", "invertebrate", "predator"})
    """A denless land predator descended from squid."""

    COMBAT_CAPABLE = True
    COMBAT_POWER = 3
    MAX_COMBAT_HEALTH = 4
    BODY_SIZE = 2
    ALLOWED_TERRAINS = LAND_TERRAINS
    REPRODUCTION_MEAL_THRESHOLD = 10
    HUNGER_INTERVAL = 260.0
    STARVATION_INTERVAL = 120.0
    HUNT_RANGE = 8
    HUNT_PREY_TYPES = Therapsid.HUNT_PREY_TYPES + (Ape, GigaSlug)
    SCAVENGE_PREY_TYPES = HUNT_PREY_TYPES
    PREDATOR_NAME = "Land Kraken"
    REPRODUCTION_BLOCKS_SET_BEHAVIOR = True
    REPRODUCTION_BLOCKS_RESET_MEALS = True

    def __init__(self, x, y):
        super().__init__(
            x,
            y,
            color=(105, 80, 125),
            allowed_terrains=LandKraken.ALLOWED_TERRAINS,
            move_cooldown=0.40,
            sprite="land_kraken",
        )
        self.configure_hunger(LandKraken.HUNGER_INTERVAL, LandKraken.STARVATION_INTERVAL)

    def get_reproduction_blocking_types(self):
        return (LandKraken,)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)
