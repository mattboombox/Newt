from .whale import Whale


class SpermWhale(Whale):
    CRITTER_TAGS = frozenset({"animal", "aquatic", "vertebrate", "predator", "apex"})
    # Sperm whales are the largest movers in the simulation and can shove
    # every smaller critter, including sailors and liches.
    BODY_SIZE = 5
    ALLOWED_TERRAINS = Whale.ALLOWED_TERRAINS
    REPRODUCTION_MEAL_THRESHOLD = Whale.REPRODUCTION_MEAL_THRESHOLD
    HUNGER_INTERVAL = Whale.HUNGER_INTERVAL
    STARVATION_INTERVAL = 220.0
    MOVE_COOLDOWN = 0.28
    PREDATOR_NAME = "Sperm Whale"

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = (95, 105, 125)
        self.allowed_terrains = SpermWhale.ALLOWED_TERRAINS
        self.move_cooldown = SpermWhale.MOVE_COOLDOWN
        self.sprite = "sperm_whale"
        self.configure_hunger(SpermWhale.HUNGER_INTERVAL, SpermWhale.STARVATION_INTERVAL)

    def get_priority_prey_types(self):
        from .squid import Squid

        return (Squid,)

    def get_hunt_prey_types(self):
        from .ape_sailor import ApeSailor
        from .sea_scorpion import SeaScorpion
        from .squid import Squid

        from .land_kraken import LandKraken

        return (Squid, LandKraken, SeaScorpion, ApeSailor)

    def get_scavenge_prey_types(self):
        return self.get_hunt_prey_types()

    def can_displace_critter(self, critter):
        return critter is not self

    def should_remove_on_failed_displacement(self, critter):
        # Shoving an occupant should never turn into an incidental kill when
        # there is no valid neighboring tile available.
        return False

    def try_scavenge_corpse(self, game):
        # A hungry whale must use its sonar to hunt live prey instead of
        # committing to a potentially distant corpse.  Nearby corpses are
        # still consumed by the base class's quick adjacent check.
        if self.is_hungry:
            return False

        return super().try_scavenge_corpse(game)

    def try_handle_priority_behavior(self, game):
        # Sperm whales actively patrol for ocean predators instead of waiting
        # for hunger, making them meaningful control on squid populations.
        return self.hunt_nearest_prey(
            game,
            self.get_hunt_prey_types(),
            self.get_predator_name(),
        )

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_meal_based_plankton_remains(game, tile)
