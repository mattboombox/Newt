from tile import Tile


class World:
    def __init__(self, cols, rows, default_terrain="ocean"):
        self.cols = cols
        self.rows = rows
        self.board = self.make_board(default_terrain)

    def make_board(self, default_terrain):
        return [
            [Tile(x, y, default_terrain) for y in range(self.rows)]
            for x in range(self.cols)
        ]

    def get_tile(self, x, y):
        if 0 <= x < self.cols and 0 <= y < self.rows:
            return self.board[x][y]
        return None

    def get_tile_at_pixel(self, mx, my, tile_size):
        x = mx // tile_size
        y = my // tile_size
        return self.get_tile(x, y)

    def get_neighbors_cardinal(self, x, y):
        neighbors = []
        for dx, dy in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
            tile = self.get_tile(x + dx, y + dy)
            if tile is not None:
                neighbors.append(tile)
        return neighbors

    def get_neighbors_all(self, x, y):
        neighbors = []
        for dx in [-1, 0, 1]:
            for dy in [-1, 0, 1]:
                if dx == 0 and dy == 0:
                    continue
                tile = self.get_tile(x + dx, y + dy)
                if tile is not None:
                    neighbors.append(tile)
        return neighbors

    def is_adjacent_to_terrain(self, x, y, terrain_names, cardinal_only=False):
        neighbors = self.get_neighbors_cardinal(x, y) if cardinal_only else self.get_neighbors_all(x, y)
        return any(tile.terrain in terrain_names for tile in neighbors)

    def count_terrain_in_radius(self, x, y, terrain_names, radius=1):
        count = 0
        for dx in range(-radius, radius + 1):
            for dy in range(-radius, radius + 1):
                tile = self.get_tile(x + dx, y + dy)
                if tile is None:
                    continue
                if tile.terrain in terrain_names:
                    count += 1
        return count

    def get_edge_tiles(self):
        edge_tiles = []

        for x in range(self.cols):
            top = self.get_tile(x, 0)
            bottom = self.get_tile(x, self.rows - 1)
            if top is not None:
                edge_tiles.append(top)
            if bottom is not None and bottom is not top:
                edge_tiles.append(bottom)

        for y in range(1, self.rows - 1):
            left = self.get_tile(0, y)
            right = self.get_tile(self.cols - 1, y)
            if left is not None:
                edge_tiles.append(left)
            if right is not None and right is not left:
                edge_tiles.append(right)

        return edge_tiles