from terrain import terrainLib

class Tile:
    def __init__ (self, x: int, y: int):
        self.x = x
        self.y = y
        self.terrain = terrainLib["ocean"]
        self.critter = None
        self.structure = None

    def getColor(self):
        #critter > structure > terrain
        if(self.critter is not None):
            return self.critter.color
        elif(self.structure is not None):
            return self.structure.color
        else:
            return self.terrain.color
