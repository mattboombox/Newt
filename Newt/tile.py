from terrain import terrainLib

class Tile:
    def __init__ (self, x: int, y: int):
        self.x = x
        self.y = y
        self.terrain = terrainLib["grass"]()
        self.critter = None
        self.structure = None

    def getThingColor(self):
        if(self.critter is not None):
            return self.critter.color
        
    def getTerrainColor(self):
        return self.terrain.color