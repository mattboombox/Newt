class terrain:
    def __init__(self, name, moveCost, fertility, color, type):
        self.name = name
        self.moveCost = moveCost
        self.fertility = fertility
        self.color = color
        self.type = type

#Terrain types       
SOLID = 1
LIQUID = 2

terrainLib = {
    "grass": terrain("grass", moveCost=2, fertility=2, color=(34, 139, 34), type=SOLID),
    "lake": terrain("lake", moveCost=1, fertility=1, color=(34, 20, 150), type=LIQUID)
}