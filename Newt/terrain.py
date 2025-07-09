class Terrain:
    def __init__(self, name, moveCost, fertility, color, type):
        self.name = name
        self.moveCost = moveCost
        self.fertility = fertility
        self.color = color
        self.type = type

#Terrain types       
SOLID = 1
LIQUID = 2
IMPASSIBLE = 3

terrainLib = {
    "grass":   lambda: Terrain("grass",   1, 2, (34,  90, 34),  SOLID),
    "desert":  lambda: Terrain("desert",  1, 0, (196, 175, 141), SOLID),
    "lake":    lambda: Terrain("lake",    1, 2, (34,  20, 200), LIQUID),
    "ocean":   lambda: Terrain("ocean",   2, 1, (34,  20, 150), LIQUID),
    "mountain":lambda: Terrain("mountain",0, 1, (64,  63, 61),  IMPASSIBLE),
    "void":    lambda: Terrain("void",    1, 0, (0,   0,  0),   LIQUID),
}

