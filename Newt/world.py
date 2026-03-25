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
        for dx, dy in [(1,0), (-1,0), (0,1), (0,-1)]:
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