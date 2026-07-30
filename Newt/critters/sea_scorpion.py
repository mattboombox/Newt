from .critter import AQUATIC_TERRAINS, Critter


class SeaScorpion(Critter):
    """A swift coastal predator descended from the trilobite branch."""

    DISPLACEMENT_LEVEL = 2
    ALLOWED_TERRAINS = AQUATIC_TERRAINS - {"lake", "trench"}
    REPRODUCTION_MEAL_THRESHOLD = 8
    HUNGER_INTERVAL = 110.0
    STARVATION_INTERVAL = 120.0
    HUNT_RANGE = 4
    PREDATOR_NAME = "Sea Scorpion"

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(145, 95, 75),
            allowed_terrains=SeaScorpion.ALLOWED_TERRAINS,
            move_cooldown=0.70,
            sprite="sea_scorpion",
        )
        self.configure_hunger(SeaScorpion.HUNGER_INTERVAL, SeaScorpion.STARVATION_INTERVAL)

    def get_hunt_prey_types(self):
        from .crab import Crab
        from .fish import Fish
        from .trilobite import Trilobite
        from .jelly_fish import Jellyfish

        return (Crab, Trilobite, Fish, Jellyfish)

    def get_scavenge_prey_types(self):
        return self.get_hunt_prey_types()

    def take_hungry_action(self, game):

        if self.hunt_nearest_prey(
            game,
            self.get_hunt_prey_types(),
            self.get_predator_name(),
        ):
            return

    def try_reproduce(self, world):
        current_tile = world.get_tile(self.x, self.y)
        if current_tile is None or current_tile.terrain != "shallows":
            return self.fail_reproduction_attempt(reset_meals=True)

        return super().try_reproduce(world)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_meal_based_plankton_remains(game, tile)
