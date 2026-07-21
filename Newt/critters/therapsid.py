from .crab import Crab
from .critter import Critter, NON_ARCTIC_LAND_TERRAINS


class Therapsid(Critter):
    ALLOWED_TERRAINS = NON_ARCTIC_LAND_TERRAINS | {"shallows"}
    FEED_TERRAINS = {"grass", "lake"}
    HUNGER_INTERVAL = 34.0
    STARVATION_INTERVAL = 40.0
    HUNT_PREY_TYPES = (Crab,)
    SCAVENGE_PREY_TYPES = (Critter,)
    REPRODUCTION_BLOCKS_SET_BEHAVIOR = True
    REPRODUCTION_BLOCKS_RESET_MEALS = True

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(150, 125, 105),
            allowed_terrains=Therapsid.ALLOWED_TERRAINS,
            move_cooldown=0.28,
            sprite="therapsid"
        )
        self.configure_hunger(Therapsid.HUNGER_INTERVAL, Therapsid.STARVATION_INTERVAL)

    def take_hungry_action(self, game):
        if self.hunt_nearest_prey(game, self.get_hunt_prey_types(), self.get_predator_name()):
            return

        self.feed_on_nearest_terrain(game, Therapsid.FEED_TERRAINS, "seek_food", require_empty_target=True)

    def get_reproduction_blocking_types(self):
        return (Therapsid,)

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)
