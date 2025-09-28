from terrain import terrainLib

class Brush:
    def __init__(self):
        self.current = None   # Currently selected terrain (instance)
        self.keys = list(terrainLib.keys())  # Ordered list of terrain names
        self.index = 0        # Current index in keys

    def setBrush(self, terrain_name):
        if terrain_name in terrainLib:
            self.current = terrainLib[terrain_name]()  # Create new instance
            self.index = self.keys.index(terrain_name)  # Sync index
            print(f"Hand set to: {self.current.name}")
        else:
            print(f"'{terrain_name}' is not in the terrain library!")

    def cycleBrush(self, forward=True):
        """Cycle forward (O key) or backward (I key)."""
        step = 1 if forward else -1
        self.index = (self.index + step) % len(self.keys)
        key = self.keys[self.index]
        self.current = terrainLib[key]()
        print(f"Hand cycled to: {self.current.name}")

    def paint(self, board, x, y):
        # Hard OOB guard to prevent IndexError from any caller
        cols, rows = len(board), len(board[0])
        if not (0 <= x < cols and 0 <= y < rows):
            return

        if self.current is None:
            print("No terrain selected to paint with!")
            return

        # Use a fresh instance each time to avoid shared state
        board[x][y].terrain = terrainLib[self.current.name]()
        #print(f"Painted {self.current.name} at ({x}, {y})")