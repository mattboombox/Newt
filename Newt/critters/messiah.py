from .critter import Critter, LAND_TERRAINS


class Messiah(Critter):
    """An immortal land wanderer who sanctifies hostile Smashers."""

    ALLOWED_TERRAINS = LAND_TERRAINS
    COMBAT_CAPABLE = True
    COMBAT_POWER = 1
    MAX_COMBAT_HEALTH = 999
    DISPLACEMENT_LEVEL = 4
    HUNT_RANGE = 12
    PREDATOR_NAME = "Messiah"

    def __init__(self, x, y):
        super().__init__(
            x,
            y,
            color=(240, 230, 170),
            allowed_terrains=self.ALLOWED_TERRAINS,
            move_cooldown=0.34,
            sprite="messiah",
        )

    def get_hunt_prey_types(self):
        from .smasher import Smasher

        return (Smasher,)

    def get_scavenge_prey_types(self):
        return ()

    def is_valid_hunt_prey(self, critter, prey_types):
        from .smasher import SaintSmasher

        return (
            not isinstance(critter, SaintSmasher)
            and super().is_valid_hunt_prey(critter, prey_types)
        )

    def resolve_hunt_attack(self, game, prey, predator_name=None):
        from .smasher import SaintSmasher, Smasher

        if isinstance(prey, Smasher) and not isinstance(prey, SaintSmasher):
            saint = SaintSmasher.sanctify_from(prey)
            if saint is not None:
                self.set_behavior("sanctify_smasher")
                return False

        return super().resolve_hunt_attack(game, prey, predator_name)

    def try_handle_priority_behavior(self, game):
        return self.hunt_nearest_prey(
            game,
            self.get_hunt_prey_types(),
            self.get_predator_name(),
        )
