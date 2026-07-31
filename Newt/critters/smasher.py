from .critter import Critter, LAND_TERRAINS


class Smasher(Critter):
    """A player-controlled titan that crushes supernatural and ape armies."""

    ALLOWED_TERRAINS = LAND_TERRAINS
    COMBAT_CAPABLE = True
    COMBAT_POWER = 5
    MAX_COMBAT_HEALTH = 9
    DISPLACEMENT_LEVEL = 5
    HUNT_RANGE = 12
    METEOR_RANGE = 10
    METEOR_BLAST_RADIUS = 2
    MINI_METEOR_COOLDOWN = 10.0
    PLAYER_SPAWN_ONLY = True
    REPRODUCTION_MEAL_THRESHOLD = 1000
    PREDATOR_NAME = "Smasher"

    def __init__(self, x, y):
        super().__init__(
            x,
            y,
            color=(135, 95, 85),
            allowed_terrains=self.ALLOWED_TERRAINS,
            move_cooldown=0.36,
            sprite="smasher",
        )
        self.mini_meteor_cooldown = 0.0

    def get_hunt_prey_types(self):
        from .ape import Ape
        from .lich import Lich, UndeadFollower

        return (Ape, Lich, UndeadFollower)

    def get_scavenge_prey_types(self):
        return ()

    def get_meteor_target(self, game):
        prey_types = self.get_hunt_prey_types()
        candidates = []

        for critter in game.critters:
            if (
                critter is self
                or critter.current_behavior == "dying"
                or not self.is_valid_hunt_prey(critter, prey_types)
            ):
                continue

            distance = self.get_tile_distance(
                game.world,
                self.x,
                self.y,
                critter.x,
                critter.y,
            )
            if self.METEOR_BLAST_RADIUS < distance <= self.METEOR_RANGE:
                candidates.append((distance, critter.id, critter))

        if not candidates:
            return None

        return min(candidates, key=lambda candidate: candidate[:2])[2]

    def try_drop_mini_meteor(self, game):
        if self.mini_meteor_cooldown > 0:
            return False

        target = self.get_meteor_target(game)
        if target is None:
            return False

        from impact import trigger_mini_meteor

        if not trigger_mini_meteor(game, target.x, target.y):
            return False

        self.mini_meteor_cooldown = self.MINI_METEOR_COOLDOWN
        self.set_behavior("drop_mini_meteor")
        return True

    def try_handle_priority_behavior(self, game):
        if self.try_drop_mini_meteor(game):
            return True

        return self.hunt_nearest_prey(
            game,
            self.get_hunt_prey_types(),
            self.get_predator_name(),
        )

    def update(self, game, dt):
        self.mini_meteor_cooldown = max(
            0.0,
            self.mini_meteor_cooldown - dt,
        )
        super().update(game, dt)


class SaintSmasher(Smasher):
    """A Smasher sanctified by Messiah to hunt only undead."""

    PREDATOR_NAME = "Saint Smasher"
    COLOR = (220, 215, 160)
    SPRITE = "saint_smasher"

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = self.COLOR
        self.sprite = self.SPRITE

    @classmethod
    def sanctify_from(cls, critter):
        if not isinstance(critter, Smasher) or isinstance(critter, cls):
            return None

        critter.__class__ = cls
        critter.color = cls.COLOR
        critter.sprite = cls.SPRITE
        critter.configure_combat()
        critter.set_behavior("sanctified")
        return critter

    def get_hunt_prey_types(self):
        from .lich import UndeadFollower

        return (UndeadFollower,)
