import random


class Critter:
    _next_id = 1

    def __init__(self, x, y, color=(255, 200, 40)):
        self.id = Critter._next_id
        Critter._next_id += 1
        self.x = x
        self.y = y
        self.color = color
        self.move_cooldown = 0.25
        self.move_timer = 0.0

    def update(self, world, dt):
        self.move_timer += dt
        if self.move_timer < self.move_cooldown:
            return

        self.move_timer = 0.0
        self.try_wander(world)

    def try_wander(self, world):
        directions = [
            (1, 0), (-1, 0),
            (0, 1), (0, -1),
        ]
        random.shuffle(directions)

        for dx, dy in directions:
            nx = self.x + dx
            ny = self.y + dy

            tile = world.get_tile(nx, ny)
            if tile is None:
                continue
            if not tile.is_walkable():
                continue
            if tile.critter is not None:
                continue

            old_tile = world.get_tile(self.x, self.y)
            if old_tile is not None:
                old_tile.critter = None

            self.x = nx
            self.y = ny
            tile.critter = self
            return