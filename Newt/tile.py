from terrain import TERRAIN_DATA


class Tile:
    def __init__(self, x, y, terrain="ocean"):
        self.x = x
        self.y = y
        self.terrain = terrain
        self.critter = None

    def set_terrain(self, terrain_name):
        if terrain_name in TERRAIN_DATA:
            self.terrain = terrain_name

    def get_color(self):
        return TERRAIN_DATA[self.terrain]["color"]

    def is_walkable(self):
        return TERRAIN_DATA[self.terrain]["walkable"]

    def describe(self):
        print(f"Tile ({self.x}, {self.y}) - terrain: {self.terrain}")
