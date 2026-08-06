from .critter import AQUATIC_TERRAINS, Critter, PreyRule


class Fish(Critter):
    CRITTER_TAGS = frozenset({"animal", "aquatic", "vertebrate", "predator"})
    ALLOWED_TERRAINS = AQUATIC_TERRAINS - {"trench"}
    REPRODUCTION_MEAL_THRESHOLD = 3
    HUNGER_INTERVAL = 40.0
    STARVATION_INTERVAL = 40.0
    FLEE_DETECTION_RADIUS = 5
    # Keep a hungry school from searching the entire large map every time it
    # moves.  Food farther away is reconsidered on the next decision tick.
    HUNT_RANGE = 12
    SCAVENGE_RANGE = 12
    HUNT_PREY_RULE = PreyRule(required_tags={"micro_food"})
    SCAVENGE_PREY_RULE = HUNT_PREY_RULE
    PREDATOR_NAME = "Fish"

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(80, 180, 255),
            allowed_terrains=Fish.ALLOWED_TERRAINS,
            move_cooldown=0.18,
            sprite="fish"
        )
        self.configure_hunger(Fish.HUNGER_INTERVAL, Fish.STARVATION_INTERVAL)

    def get_flee_predator_types(self):
        from .shark import Shark
        from .squid import Squid
        from .sea_scorpion import SeaScorpion

        return (Shark, Squid, SeaScorpion)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_meal_based_plankton_remains(game, tile)
