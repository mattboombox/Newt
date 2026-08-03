from .critter import Critter, LAND_TERRAINS, PreyRule
from .crab import Crab
from .newt import Newt
from .snail import Snail

class Therapsid(Critter):
    CRITTER_TAGS = frozenset({"animal", "terrestrial", "vertebrate", "predator"})
    BODY_SIZE = 2
    ALLOWED_TERRAINS = LAND_TERRAINS
    HUNGER_INTERVAL = 34.0
    STARVATION_INTERVAL = 40.0
    HUNT_RANGE = 18
    HUNT_PREY_RULE = PreyRule(
        required_tags={"animal"},
        excluded_tags={"micro_food", "protected"},
        max_body_size=1,
    )
    SCAVENGE_PREY_RULE = PreyRule(
        required_tags={"animal"},
        excluded_tags={"protected"},
    )
    PRIORITY_PREY_TYPES = (Crab, Newt, Snail)
    REPRODUCTION_BLOCKS_SET_BEHAVIOR = True
    REPRODUCTION_BLOCKS_RESET_MEALS = True
    REPRODUCTION_MEAL_THRESHOLD = 4

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(150, 125, 105),
            allowed_terrains=Therapsid.ALLOWED_TERRAINS,
            move_cooldown=0.28,
            sprite="therapsid"
        )
        self.configure_hunger(Therapsid.HUNGER_INTERVAL, Therapsid.STARVATION_INTERVAL)

    def get_reproduction_blocking_types(self):
        return (Therapsid,)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)
