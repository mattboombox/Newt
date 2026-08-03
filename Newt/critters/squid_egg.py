from .critter import AQUATIC_TERRAINS, Critter
from .plankton import Plankton


class SquidEgg(Critter):
    CRITTER_TAGS = frozenset({"animal", "aquatic", "egg", "protected"})
    BODY_SIZE = 0
    ALLOWED_TERRAINS = AQUATIC_TERRAINS
    MOVE_COOLDOWN = Plankton.MOVE_COOLDOWN

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(220, 200, 255),
            allowed_terrains=SquidEgg.ALLOWED_TERRAINS,
            move_cooldown=SquidEgg.MOVE_COOLDOWN,
            sprite="squid_egg"
        )

    def try_reproduce(self, world):
        return None

    def can_be_eaten_by(self, predator):
        from .jelly_fish import Jellyfish

        return isinstance(predator, Jellyfish)

    def hatch(self, game):
        from .squid import Squid

        tile = game.world.get_tile(self.x, self.y)
        squid = Squid(self.x, self.y)
        if tile is not None:
            tile.critter = squid

        if self in game.critters:
            game.critters.remove(self)
        game.critters.append(squid)

    def update(self, game, dt):
        from .squid import Squid

        if self.current_behavior == "dying":
            self.update_dying(game, dt)
            return

        nearby_hatch_trigger = self.find_nearby_critters(
            game.world,
            Squid.HUNT_PREY_RULE,
            1,
        )
        if nearby_hatch_trigger:
            self.hatch(game)
            return

        self.move_timer += dt
        if self.move_timer < self.move_cooldown:
            return

        self.move_timer = 0.0
        self.try_wander(game.world, game)
