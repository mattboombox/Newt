import random
import terrain

class tile:
    def __init__ (self, posX, posY):
        self.posX = posX
        self.posY = posY
        self.terrain = terrain.terrainLib["ocean"]
        self.structureID = None
        self.critterID = None
        #print("Tile init", self.terrain.name, self.posX, self.posY)

    #def getcolor():
        #critter > structure > terrain
        #if(self.critterID):
            #get unit color
        #elif(self.structureID):
            #get structure color
       #elif(self.terrainID):
            #get terrain color
