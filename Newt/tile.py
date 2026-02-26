from terrain import terrainLib


class Tile:
    __slots__ = ("x", "y", "terrain", "critter", "structure", "terrain_color")

    def __init__(self, x: int, y: int):
        self.x = x
        self.y = y

        self.terrain = terrainLib["ocean"]()
        self.critter = None
        self.structure = None

        # Cache color so drawing doesn't chase through terrain objects every time
        self.terrain_color = self.terrain.color

    # ---- Terrain helpers ----
    def setTerrain(self, terrain_obj) -> None:
        """Use this whenever you change terrain so cached color stays correct."""
        self.terrain = terrain_obj
        self.terrain_color = terrain_obj.color

    def setTerrainByName(self, name: str) -> None:
        """Convenience: set terrain from terrainLib key."""
        t = terrainLib[name]()
        self.setTerrain(t)

    # ---- Color getters (kept for compatibility) ----
    def getThingColor(self):
        if self.critter is not None:
            return self.critter.color
        return None

    def getTerrainColor(self):
        return self.terrain_color

    # ---- Debug ----
    def describe(self):
        print(f"Position: {self.x}, {self.y}")
        print(f"Terrain: {self.terrain.name}")
        if self.critter is not None:
            print(f"Critter: {self.critter.name} {self.critter.color}  (fish={self.critter.fish})")
        print()