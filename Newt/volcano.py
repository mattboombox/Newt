import random


class Volcano:
    def __init__(self, x, y, state="active"):
        self.x = x
        self.y = y
        self.state = state

        self.eruption_timer = 0.0
        self.eruption_interval = 1.0

        self.dormancy_timer = 0.0
        self.dormancy_interval = 8.0

        self.reawaken_chance = 0.02
        self.extinction_chance = 0.08
        self.chain_chance = 0.75
        self.lava_radius = 4

    def update(self, game, dt):
        if self.state == "active":
            self.eruption_timer += dt
            self.dormancy_timer += dt

            if self.eruption_timer >= self.eruption_interval:
                self.eruption_timer = 0.0
                self.erupt(game.world)

            if self.dormancy_timer >= self.dormancy_interval:
                self.go_dormant(game)

        elif self.state == "dormant":
            if random.random() < self.reawaken_chance * dt:
                self.reawaken(game)
                return

            if random.random() < self.extinction_chance * dt:
                self.go_extinct(game)
                return

    def erupt(self, world):
        center_tile = world.get_tile(self.x, self.y)
        if center_tile is not None:
            center_tile.set_terrain("active_volcano")

        for dx in range(-self.lava_radius, self.lava_radius + 1):
            for dy in range(-self.lava_radius, self.lava_radius + 1):
                if dx == 0 and dy == 0:
                    continue

                if dx * dx + dy * dy > self.lava_radius * self.lava_radius:
                    continue

                tile = world.get_tile(self.x + dx, self.y + dy)
                if tile is None:
                    continue

                if tile.terrain in ("mountain", "active_volcano", "dormant_volcano"):
                    continue

                tile.set_terrain("lava")

    def go_dormant(self, game):
        self.state = "dormant"
        self.eruption_timer = 0.0
        self.dormancy_timer = 0.0

        center_tile = game.world.get_tile(self.x, self.y)
        if center_tile is not None:
            center_tile.set_terrain("dormant_volcano")

    def reawaken(self, game):
        self.state = "active"
        self.eruption_timer = 0.0
        self.dormancy_timer = 0.0

        center_tile = game.world.get_tile(self.x, self.y)
        if center_tile is not None:
            center_tile.set_terrain("active_volcano")

    def go_extinct(self, game):
        center_tile = game.world.get_tile(self.x, self.y)
        if center_tile is not None:
            center_tile.set_terrain("mountain")

        self.try_chain_spawn(game)
        self.remove_from_game(game)

    def try_chain_spawn(self, game):
        neighbors = game.world.get_neighbors_all(self.x, self.y)
        random.shuffle(neighbors)

        valid_tiles = []
        for tile in neighbors:
            if tile.terrain not in ("active_volcano", "dormant_volcano"):
                valid_tiles.append(tile)

        if not valid_tiles:
            return

        if random.random() < self.chain_chance:
            tile = random.choice(valid_tiles)
            self.spawn_new_volcano(game, tile.x, tile.y)

    def spawn_new_volcano(self, game, x, y):
        tile = game.world.get_tile(x, y)
        if tile is None:
            return

        tile.set_terrain("active_volcano")
        game.volcanoes.append(Volcano(x, y, state="active"))

    def remove_from_game(self, game):
        if self in game.volcanoes:
            game.volcanoes.remove(self)