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
    "grass": lambda: Terrain("grass", 1, 2, (62,92,32), SOLID),
    "lake": lambda: Terrain("lake", 1, 2, (122, 171, 250), LIQUID),
    "ocean": lambda: Terrain("ocean", 2, 1, (34,  20, 150), LIQUID),
    "mountain":lambda: Terrain("mountain",0, 1, (64,  63, 61), IMPASSIBLE),
    "activeVolcano": lambda: Terrain("activeVolcano", 1, 0, (255,  63, 61), IMPASSIBLE),
    "dormantVolcano": lambda: Terrain("dormantVolcano", 1, 0, (233, 238, 247), IMPASSIBLE),
    "lava": lambda: Terrain("lava", 1, 0, (200,  63, 61), LIQUID),
    "stone": lambda: Terrain("stone", 1, 0, (87, 85, 81), SOLID),
    "beach": lambda: Terrain("beach", 1, 0, (196, 175, 141), SOLID),
    "soil": lambda: Terrain("soil", 1, 0, (97, 78, 52), SOLID),
}