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
    "grass": Terrain("grass", moveCost=1, fertility=2, color=(34, 139, 34), type=SOLID),
    "desert": Terrain("desert", moveCost=1, fertility=0, color=(196, 175, 141), type=SOLID),
    "lake": Terrain("lake", moveCost=1, fertility=2, color=(34, 20, 200), type=LIQUID),
    "ocean": Terrain("ocean", moveCost=2, fertility=1, color=(34, 20, 150), type=LIQUID),
    "mountain": Terrain("mountain", moveCost=0, fertility=1, color=(64, 63, 61), type=IMPASSIBLE),
}

