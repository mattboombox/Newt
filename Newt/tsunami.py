class Tsunami:
    def __init__(self, x, y, max_steps=12, interval=0.2):
        self.interval = interval
        self.timer = 0.0
        self.max_steps = max_steps
        self.steps = 0

        # Tiles currently at the wave front
        self.frontier = {(x, y)}

        # Water tiles currently being shown as tsunami.
        # This is visual-only state; real terrain stays untouched.
        self.current_ring = set()

        # Prevent revisiting the same place forever
        self.visited = {(x, y)}

    def update(self, game, dt):
        self.timer += dt
        if self.timer < self.interval:
            return

        self.timer = 0.0

        self.steps += 1
        still_active = self.expand(game.world)

        if not still_active or self.steps >= self.max_steps:
            self.remove_from_game(game)

    def expand(self, world):
        next_frontier = set()
        current_ring = set()

        for x, y in self.frontier:
            for dx, dy in [
                (1, 0), (-1, 0),
                (0, 1), (0, -1),
                (1, 1), (1, -1),
                (-1, 1), (-1, -1),
            ]:
                nx = x + dx
                ny = y + dy

                if (nx, ny) in self.visited:
                    continue

                tile = world.get_tile(nx, ny)
                if tile is None:
                    continue

                self.visited.add((nx, ny))

                # Water keeps propagating
                if tile.terrain in ("ocean", "shallows", "lake"):
                    current_ring.add((nx, ny))
                    next_frontier.add((nx, ny))
                    continue

                # Hard blockers: stop this branch
                if tile.terrain in ("mountain", "active_volcano", "dormant_volcano"):
                    continue

                # Land gets hit once, becomes shallows, and this branch stops there
                tile.set_terrain("shallows")

        self.current_ring = current_ring
        self.frontier = next_frontier

        return len(self.frontier) > 0

    def remove_from_game(self, game):
        self.current_ring.clear()

        if self in game.tsunamis:
            game.tsunamis.remove(self)
