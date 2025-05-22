from enum import Enum

class weapon:
    def __init__(self, name, damage, damageType, consumption):
        self.name = name
        self.damage = damage
        self.damageType = damageType
        self.consumption = consumption
        #self.cost = cost

class damageTypes(Enum):
    BLUNT = 0
    SHARP = 1
    EXPLOSIVE = 3
    BALLISTIC = 4

class consumptionTypes(Enum):
    NONE = 0
    BULLET = 1
    FUEL = 2
    EXPLOSIVE = 3
    ELECTRIC = 4

toolLib = {
    "club": weapon("club", damage=2, damageType=damageTypes.BLUNT, consumption=consumptionTypes.NONE),
    "spear": weapon("spear", damage=2, damageType=damageTypes.SHARP, consumption=consumptionTypes.NONE),
    "sword": weapon("sword", damage=3, damageType=damageTypes.SHARP, consumption=consumptionTypes.NONE),
    "sling": weapon("sling", damage=2, damageType=damageTypes.BALLISTIC, consumption=consumptionTypes.NONE),
    "bow": weapon("bow", damage=3, damageType=damageTypes.BALLISTIC, consumption=consumptionTypes.NONE),
    "musket": weapon("musket", damage=3, damageType=damageTypes.BALLISTIC, consumption=consumptionTypes.BULLET),
}