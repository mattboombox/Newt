from .critter import Critter


class SquidEgg(Critter):
    ALLOWED_TERRAINS = {"ocean", "trench", "shallows", "lake"}
    MOVE_COOLDOWN = 4.20

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
        from .crab import Crab
        from .fish import Fish

        if self.current_behavior == "dying":
            self.update_dying(game, dt)
            return

        nearby_hatch_trigger = self.find_nearby_critters(game.world, (Fish, Crab), 1)
        if nearby_hatch_trigger:
            self.hatch(game)
            return

        self.move_timer += dt
        if self.move_timer < self.move_cooldown:
            return

        self.move_timer = 0.0
        self.try_wander(game.world, game)
