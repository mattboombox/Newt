from .critter import Critter


class Plankton(Critter):
    ALLOWED_TERRAINS = {"ocean", "trench", "shallows"}
    HUNGER_INTERVAL = 15.0
    STARVATION_INTERVAL = 8.0
    REPRODUCTION_BLOCKS_RESET_MEALS = True
    REPRODUCTION_MEAL_THRESHOLD = 3

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(160, 255, 180),
            allowed_terrains=Plankton.ALLOWED_TERRAINS,
            move_cooldown=4.20,
            sprite="plankton"
        )
        self.configure_hunger(Plankton.HUNGER_INTERVAL, Plankton.STARVATION_INTERVAL)

    def take_hungry_action(self, game):
        self.handle_successful_meal(game)

    def get_reproduction_blocking_types(self):
        return (Plankton,)
