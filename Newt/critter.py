import random


class Critter:
    _next_id = 1

    def __init__(
        self,
        x,
        y,
        color=(255, 200, 40),
        allowed_terrains=None,
        required_tags=None,
        sprite=None,
        move_cooldown=0.25,
    ):
        self.id = Critter._next_id
        Critter._next_id += 1

        self.x = x
        self.y = y
        self.color = color
        self.sprite = sprite

        self.move_cooldown = move_cooldown
        self.move_timer = 0.0

        self.allowed_terrains = allowed_terrains
        self.required_tags = required_tags or set()

    def update(self, world, dt):
        self.move_timer += dt
        if self.move_timer < self.move_cooldown:
            return

        self.move_timer = 0.0
        self.try_wander(world)

    def can_enter_tile(self, tile):
        if tile is None:
            return False

        if tile.critter is not None:
            return False

        if self.allowed_terrains is not None and tile.terrain not in self.allowed_terrains:
            return False

        for tag in self.required_tags:
            if not tile.has_tag(tag):
                return False

        return True

    def move_to(self, world, nx, ny):
        tile = world.get_tile(nx, ny)
        if not self.can_enter_tile(tile):
            return False

        old_tile = world.get_tile(self.x, self.y)
        if old_tile is not None:
            old_tile.critter = None

        self.x = nx
        self.y = ny
        tile.critter = self
        return True

    def try_wander(self, world):
        directions = [
            (1, 0), (-1, 0),
            (0, 1), (0, -1),
        ]
        random.shuffle(directions)

        for dx, dy in directions:
            nx = (self.x + dx) % world.cols
            ny = self.y + dy

            tile = world.get_tile(nx, ny)
            if self.can_enter_tile(tile):
                self.move_to(world, nx, ny)
                return


class Crab(Critter):
    ALLOWED_TERRAINS = {"beach", "shallows"}

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(255, 80, 80),
            allowed_terrains=Crab.ALLOWED_TERRAINS,
            move_cooldown=0.30,
            sprite="crab"
        )


class Fish(Critter):
    ALLOWED_TERRAINS = {"ocean", "shallows", "lake"}

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(80, 180, 255),
            allowed_terrains=Fish.ALLOWED_TERRAINS,
            move_cooldown=0.22,
            sprite="fish"
        )


CRITTER_TYPES = {
    "crab": Crab,
    "fish": Fish,
}

CRITTER_ORDER = list(CRITTER_TYPES.keys())
