from .ape import Ape
from .critter import Critter
from .fish import Fish
from .newt import Newt


class Eagle(Critter):
    """A fast, terrain-independent aerial predator."""

    ALLOWED_TERRAINS = None
    DISPLACEMENT_LEVEL = 5
    REPRODUCTION_MEAL_THRESHOLD = 8
    HUNGER_INTERVAL = 20.0
    STARVATION_INTERVAL = 240.0
    HUNT_RANGE = 24
    HUNT_PREY_TYPES = (Ape, Fish, Newt)
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

    def take_hungry_action(self, game):
        if self.hunt_nearest_prey(
            game,
            self.get_hunt_prey_types(),
            self.get_predator_name(),
        ):
            return

        self.try_wander(game.world, game)
