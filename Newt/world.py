import random

from tile import Tile


class World:
    def __init__(self, cols, rows, default_terrain="ocean"):
        self.cols = cols
        self.rows = rows
        self.erosion_chunk_size = 16
        self._building_tiles = set()
        self._occupied_critter_tiles = set()
        self.board = self.make_board(default_terrain)
        self._erodible_chunks = {}
        self.rebuild_erosion_chunks()

    def make_board(self, default_terrain):
        return [
            [Tile(x, y, default_terrain, world=self) for y in range(self.rows)]
            for x in range(self.cols)
        ]

    def get_chunk_key(self, x, y):
        return x // self.erosion_chunk_size, y // self.erosion_chunk_size

    def rebuild_erosion_chunks(self):
        from erosion import is_erodible_terrain

        self._erodible_chunks = {}
        for x in range(self.cols):
            for y in range(self.rows):
                tile = self.board[x][y]
                if not is_erodible_terrain(tile.terrain):
                    continue

                chunk_key = self.get_chunk_key(x, y)
                self._erodible_chunks.setdefault(chunk_key, set()).add(tile)

    def on_tile_terrain_changed(self, tile, old_terrain, new_terrain):
        from erosion import is_erodible_terrain

        old_is_erodible = is_erodible_terrain(old_terrain)
        new_is_erodible = is_erodible_terrain(new_terrain)
        if old_is_erodible == new_is_erodible:
            return

        chunk_key = self.get_chunk_key(tile.x, tile.y)

        if old_is_erodible:
            chunk_tiles = self._erodible_chunks.get(chunk_key)
            if chunk_tiles is not None:
                chunk_tiles.discard(tile)
                if not chunk_tiles:
                    del self._erodible_chunks[chunk_key]

        if new_is_erodible:
            self._erodible_chunks.setdefault(chunk_key, set()).add(tile)

    def on_tile_building_changed(self, tile, old_building, new_building):
        if old_building is not None:
            self._building_tiles.discard(tile)
        if new_building is not None:
            self._building_tiles.add(tile)

    def on_tile_critter_changed(self, tile, old_critter, new_critter):
        if old_critter is not None:
            self._occupied_critter_tiles.discard(tile)
        if new_critter is not None:
            self._occupied_critter_tiles.add(tile)

    def get_building_tiles(self):
        return tuple(
            sorted(self._building_tiles, key=lambda tile: (tile.x, tile.y))
        )

    def get_occupied_critter_tiles(self):
        return tuple(
            sorted(
                self._occupied_critter_tiles,
                key=lambda tile: (tile.x, tile.y),
            )
        )

    def get_random_erodible_tile(self):
        if not self._erodible_chunks:
            return None

        chunk_key = random.choice(tuple(self._erodible_chunks.keys()))
        chunk_tiles = self._erodible_chunks[chunk_key]
        if not chunk_tiles:
            del self._erodible_chunks[chunk_key]
            return self.get_random_erodible_tile()

        return random.choice(tuple(chunk_tiles))

    def get_tile(self, x, y):
        if 0 <= x < self.cols and 0 <= y < self.rows:
            return self.board[x][y]
        return None

    def get_wrapped_tile(self, x, y):
        """Return a tile with horizontal wrapping and bounded polar edges."""
        if self.cols <= 0 or not 0 <= y < self.rows:
            return None
        return self.board[x % self.cols][y]

    def get_wrapped_neighbors_all(self, x, y):
        neighbors = []
        seen = set()
        for dx in (-1, 0, 1):
            for dy in (-1, 0, 1):
                if dx == 0 and dy == 0:
                    continue
                tile = self.get_wrapped_tile(x + dx, y + dy)
                if tile is None or (tile.x, tile.y) in seen:
                    continue
                seen.add((tile.x, tile.y))
                neighbors.append(tile)
        return neighbors

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
