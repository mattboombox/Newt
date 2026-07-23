from .critter import Critter


class SeaScorpion(Critter):
    """A swift coastal predator descended from the trilobite branch."""

    ALLOWED_TERRAINS = {"ocean", "shallows"}
    REPRODUCTION_MEAL_THRESHOLD = 8
    HUNGER_INTERVAL = 110.0
    STARVATION_INTERVAL = 80.0
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
        from .squid import Squid
        from .fish import Fish
        from .trilobite import Trilobite

        return (Crab, Trilobite, Squid, Fish)

    def get_scavenge_prey_types(self):
        return self.get_hunt_prey_types()

    def take_hungry_action(self, game):
        from .squid import Squid

        # Squid are their preferred coastal prey; other prey are a fallback.
        if self.hunt_nearest_prey(game, (Squid,), self.get_predator_name()):
            return

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
        return self.try_spawn_plankton_remains(game, tile)
