class Terrain:
    __slots__ = ("name", "moveCost", "fertility", "color", "type")

    def __init__(self, name, moveCost, fertility, color, type):
        self.name = name
        self.moveCost = moveCost
        self.fertility = fertility
        self.color = color
        self.type = type


# Terrain types
SOLID = 1
LIQUID = 2
IMPASSIBLE = 3


# Base definitions (single source of truth)
_TERRAIN_DEF = {
    "grass":          (1, 2, (75, 145, 70),  SOLID),
    "lake":           (1, 2, (122, 171, 250), LIQUID),
    "ocean":          (2, 1, (34, 20, 150),  LIQUID),
    "mountain":       (0, 1, (64, 63, 61),   IMPASSIBLE),
    "activeVolcano":  (1, 0, (255, 63, 61),  IMPASSIBLE),
    "dormantVolcano": (1, 0, (233, 238, 247), IMPASSIBLE),
    "lava":           (1, 0, (200, 63, 61),  IMPASSIBLE),
    "stone":          (1, 0, (87, 85, 81),   SOLID),
    "desert":         (1, 0, (184, 165, 105), SOLID),
    "beach":          (1, 0, (196, 175, 141), SOLID),
    "shallows":       (1, 0, (20, 85, 150),  LIQUID),
}


def makeTerrain(name: str) -> Terrain:
    """Preferred constructor."""
    moveCost, fertility, color, ttype = _TERRAIN_DEF[name]
    return Terrain(name, moveCost, fertility, color, ttype)


# Backwards-compatible factory dict: terrainLib["grass"]() still works
terrainLib = {name: (lambda n=name: makeTerrain(n)) for name in _TERRAIN_DEF}