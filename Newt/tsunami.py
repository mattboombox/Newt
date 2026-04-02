class Tsunami:
    def __init__(self, x, y, max_radius=12, interval=0.2):
        self.x = x
        self.y = y
        self.radius = 0
        self.max_radius = max_radius
        self.interval = interval
        self.timer = 0.0

        # Keep track of the previous ring so we can clear old tsunami tiles
        self.previous_ring = []

    def update(self, game, dt):
        self.timer += dt
        if self.timer < self.interval:
            return

        self.timer = 0.0

        # Clear previous visual wave tiles back to normal water
        self.clear_previous_ring(game.world)

        self.radius += 1
        self.expand(game.world)

        if self.radius >= self.max_radius:
            self.remove_from_game(game)

    def expand(self, world):
        r = self.radius
        current_ring = []

        for dx in range(-r, r + 1):
            for dy in range(-r, r + 1):
                # Only process the outer ring
                if max(abs(dx), abs(dy)) != r:
                    continue

                tile = world.get_tile(self.x + dx, self.y + dy)
                if tile is None:
                    continue

                current_ring.append(tile)

                # Wave travels through water
                if tile.terrain in ("ocean", "shallows", "lake"):
                    tile.set_terrain("tsunami")
                    continue

                # These block or ignore the wave
                if tile.terrain in ("mountain", "active_volcano", "dormant_volcano", "lava"):
                    continue

                # Land hit by tsunami becomes shallows
                tile.set_terrain("shallows")

        self.previous_ring = current_ring

    def clear_previous_ring(self, world):
        for tile in self.previous_ring:
            if tile is None:
                continue

            if tile.terrain == "tsunami":
                # Decide what tsunami water should settle back into
                # Lakes go back to lake if they are enclosed inland water,
                # otherwise default back to ocean/shallows behavior.
                if world.is_adjacent_to_terrain(tile.x, tile.y, {"lake"}):
                    tile.set_terrain("lake")
                elif world.is_adjacent_to_terrain(tile.x, tile.y, {"shallows", "beach", "sand", "grass", "stone"}):
                    tile.set_terrain("shallows")
                else:
                    tile.set_terrain("ocean")

        self.previous_ring = []

    def remove_from_game(self, game):
        # Clean up last visible wave ring before removing
        self.clear_previous_ring(game.world)

        if self in game.tsunamis:
            game.tsunamis.remove(self)