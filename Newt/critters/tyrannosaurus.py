from .critter import Critter, LAND_TERRAINS


class Tyrannosaurus(Critter):
    """A powerful land predator that hunts every huntable critter."""

    COMBAT_CAPABLE = True
    COMBAT_POWER = 5
    MAX_COMBAT_HEALTH = 7
    DISPLACEMENT_LEVEL = 4
    ALLOWED_TERRAINS = LAND_TERRAINS
    REPRODUCTION_MEAL_THRESHOLD = 10
    HUNGER_INTERVAL = 80.0
    STARVATION_INTERVAL = 160.0
    HUNT_RANGE = 18
    HUNT_PREY_TYPES = (Critter,)
    SCAVENGE_PREY_TYPES = (Critter,)
    PREDATOR_NAME = "Tyrannosaurus"
    REPRODUCTION_BLOCKS_SET_BEHAVIOR = True
    REPRODUCTION_BLOCKS_RESET_MEALS = True

    def __init__(self, x, y):
        super().__init__(
            x,
            y,
            color=(120, 105, 75),
            allowed_terrains=self.ALLOWED_TERRAINS,
            move_cooldown=0.30,
            sprite="tyrannosaurus",
        )
        self.configure_hunger(self.HUNGER_INTERVAL, self.STARVATION_INTERVAL)

    def take_hungry_action(self, game):
        if self.hunt_nearest_prey(
            game,
            self.get_hunt_prey_types(),
            self.get_predator_name(),
        ):
            return

        self.try_wander(game.world, game)

    def get_reproduction_blocking_types(self):
        return (Tyrannosaurus,)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)

