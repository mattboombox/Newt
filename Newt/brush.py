from terrain import terrainLib


class Brush:
    def __init__(self):
        self.current = None                 # terrain instance (for name only)
        self.keys = list(terrainLib.keys()) # ordered terrain names
        self.index = 0

    def setBrush(self, terrain_name: str) -> None:
        if terrain_name in terrainLib:
            self.current = terrainLib[terrain_name]()   # new instance
            self.index = self.keys.index(terrain_name)  # sync index
            print(f"Hand set to: {self.current.name}")
        else:
            print(f"'{terrain_name}' is not in the terrain library!")

    def cycleBrush(self, forward: bool = True) -> None:
        step = 1 if forward else -1
        self.index = (self.index + step) % len(self.keys)
        key = self.keys[self.index]
        self.current = terrainLib[key]()
        print(f"Hand cycled to: {self.current.name}")

    def paint(self, board, x: int, y: int) -> None:
        # OOB guard
        cols, rows = len(board), len(board[0])
        if not (0 <= x < cols and 0 <= y < rows):
            return

        if self.current is None:
            print("No terrain selected to paint with!")
            return

        tile = board[x][y]
        name = self.current.name

        # Use Tile helper if available (keeps cached color correct)
        if hasattr(tile, "setTerrainByName"):
            tile.setTerrainByName(name)
        else:
            tile.terrain = terrainLib[name]()