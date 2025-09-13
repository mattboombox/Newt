from terrain import terrainLib

class Tile:
    def __init__ (self, x: int, y: int):
        self.x = x
        self.y = y
        self.terrain = terrainLib["ocean"]()
        self.critter = None
        self.structure = None

    def getThingColor(self):
        if(self.critter is not None):
            return self.critter.color
        
    def getTerrainColor(self):
        return self.terrain.color
    
    def describe(self):
        print("Position:", self.x, self.y)
        print("Terrain:", self.terrain.name)
        if(self.critter is not None):
            print("Critter:", self.critter.name, self.critter.color)
        print()