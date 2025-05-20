import random

class tile:
    def __init__ (self, posX, posY):
        self.posX = posX
        self.posY = posY
        self.color = (random.randint(0, 255), random.randint(0, 255), random.randint(0, 255))
        #self.terrain = terrain
        #self.structure = structure
        #self.critter = critter
        print("Tile init", posX, posY)