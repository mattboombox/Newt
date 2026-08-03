from .critter import Critter, PreyRule
from .wolf import Wolf


class Herrera(Critter):
    """A wolf-like combat predator without den behavior."""

    CRITTER_TAGS = frozenset({"animal", "terrestrial", "vertebrate", "predator"})
    COMBAT_CAPABLE = Wolf.COMBAT_CAPABLE
    COMBAT_POWER = Wolf.COMBAT_POWER
    MAX_COMBAT_HEALTH = Wolf.MAX_COMBAT_HEALTH
    BODY_SIZE = Wolf.BODY_SIZE
    ALLOWED_TERRAINS = Wolf.ALLOWED_TERRAINS
    REPRODUCTION_MEAL_THRESHOLD = Wolf.REPRODUCTION_MEAL_THRESHOLD
    HUNGER_INTERVAL = Wolf.HUNGER_INTERVAL
    STARVATION_INTERVAL = Wolf.STARVATION_INTERVAL
    HUNT_RANGE = Wolf.HUNT_RANGE
    HUNT_PREY_RULE = PreyRule(
        required_tags={"animal", "terrestrial"},
        excluded_tags={"protected", "undead"},
        included_types=(Wolf,),
        max_body_size=3,
    )
    SCAVENGE_PREY_RULE = HUNT_PREY_RULE
    PREDATOR_NAME = "Herrera"
    REPRODUCTION_BLOCKS_SET_BEHAVIOR = True
    REPRODUCTION_BLOCKS_RESET_MEALS = True

    def __init__(self, x, y):
        super().__init__(
            x,
            y,
            color=(155, 125, 85),
            allowed_terrains=self.ALLOWED_TERRAINS,
            move_cooldown=0.32,
            sprite="herrera",
        )
        self.configure_hunger(self.HUNGER_INTERVAL, self.STARVATION_INTERVAL)

    def get_reproduction_blocking_types(self):
        return (Herrera,)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)
