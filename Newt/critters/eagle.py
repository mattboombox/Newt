from .critter import Critter, PreyRule
from .fish import Fish
from .newt import Newt


class Eagle(Critter):
    CRITTER_TAGS = frozenset({"animal", "terrestrial", "flying", "vertebrate", "predator"})
    """A fast, terrain-independent aerial predator."""

    ALLOWED_TERRAINS = None
    BODY_SIZE = 5
    REPRODUCTION_MEAL_THRESHOLD = 8
    HUNGER_INTERVAL = 20.0
    STARVATION_INTERVAL = 240.0
    HUNT_RANGE = 24
    HUNT_PREY_RULE = PreyRule(
        required_tags={"animal"},
        excluded_tags={"micro_food", "protected", "undead"},
        max_body_size=3,
    )
    PRIORITY_PREY_TYPES = (Fish, Newt)
    PREDATOR_NAME = "Eagle"
    MOVE_COOLDOWN = 0.06

    def __init__(self, x, y):
        super().__init__(
            x,
            y,
            color=(145, 115, 70),
            allowed_terrains=self.ALLOWED_TERRAINS,
            move_cooldown=self.MOVE_COOLDOWN,
            sprite="eagle",
        )
        self.configure_hunger(self.HUNGER_INTERVAL, self.STARVATION_INTERVAL)

    def can_be_hunted_by(self, predator):
        return False
