from .critter import Critter, LAND_TERRAINS


class Messiah(Critter):
    CRITTER_TAGS = frozenset({"divine", "terrestrial", "sapient", "protected"})
    """An immortal land wanderer who sanctifies hostile Smashers."""

    ALLOWED_TERRAINS = LAND_TERRAINS
    COMBAT_CAPABLE = True
    COMBAT_POWER = 1
    MAX_COMBAT_HEALTH = 999
    BODY_SIZE = 4
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

    @classmethod
    def recruit(cls, ape):
        from .ape import Ape

        if not Ape.is_recruitable_civilian(ape):
            return None

        ape.clear_home_building()
        ape.__class__ = cls
        ape.color = (240, 230, 170)
        ape.sprite = "messiah"
        ape.allowed_terrains = set(cls.ALLOWED_TERRAINS)
        ape.required_tags = set()
        ape.move_cooldown = 0.34
        ape.move_timer = 0.0
        ape.is_hungry = False
        ape.hunger_interval = None
        ape.starvation_interval = None
        ape.hunger_timer = None
        ape.starvation_timer = None
        ape.meals_eaten = 0
        ape.configure_combat()
        ape.set_behavior("recruited_messiah")
        return ape

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
