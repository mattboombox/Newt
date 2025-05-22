from enum import Enum

class armor:
    def __init__(self, name, AP, SP, speed, armorType, consumption):
        self.name = name
        self.AP = AP #Armor points
        self.SP = SP #Sheild points
        self.speed
        self.armorType = armorType
        self.consumption = consumption
        #self.cost = cost

class armorTypes(Enum):
    REGULAR = 0
    VEHICLE = 1
    HOVER = 2
    NAVAL = 3

class consumptionTypes(Enum):
    NONE = 0
    FOOD = 1
    FUEL = 2
    ELECTRIC = 3

armorLib = {
    #Regular
    "cloth": armor("cloth", AP=1, SP=0, speed=0, armorType = armorTypes.REGULAR, consumption=consumptionTypes.NONE),
    "pelt": armor("pelt", AP=2, SP=0, speed=0, armorType = armorTypes.REGULAR, consumption=consumptionTypes.NONE),
    "bone": armor("bone", AP=4, SP=0, speed=-1, armorType = armorTypes.REGULAR, consumption=consumptionTypes.NONE),
    "horse": armor("horse", AP=4, SP=0, speed=4, armorType = armorTypes.REGULAR, consumption=consumptionTypes.FOOD),
    "leather": armor("leather", AP=4, SP=0, speed=0, armorType = armorTypes.REGULAR, consumption=consumptionTypes.NONE),
    "scrapMetal": armor("scrapMetal", AP=5, SP=0, speed=-2, armorType = armorTypes.REGULAR, consumption=consumptionTypes.NONE),
    "iron": armor("iron", AP=6, SP=0, speed=-1, armorType = armorTypes.REGULAR, consumption=consumptionTypes.NONE),
    "steel": armor("steel", AP=8, SP=0, speed=-1, armorType = armorTypes.REGULAR, consumption=consumptionTypes.NONE),
    "flak": armor("flak", AP=4, SP=0, speed=0, armorType = armorTypes.REGULAR, consumption=consumptionTypes.NONE),
    "armoredHorse": armor("armoredHorse", AP=6, SP=0, speed=3, armorType = armorTypes.REGULAR, consumption=consumptionTypes.FOOD),
    "exoFlak": armor("exoFlak", AP=5, SP=0, speed=2, armorType = armorTypes.REGULAR, consumption=consumptionTypes.ELECTRIC),
    "heavyExo": armor("heavyExo", AP=15, SP=0, speed=1, armorType = armorTypes.REGULAR, consumption=consumptionTypes.ELECTRIC),

    #Vehicle
    "cart": armor("cart", AP=4, SP=0, speed=0, armorType = armorTypes.VEHICLE, consumption=consumptionTypes.NONE),
    "buggey": armor("buggey", AP=10, SP=0, speed=6, armorType = armorTypes.VEHICLE, consumption=consumptionTypes.FUEL),
    "tank": armor("tank", AP=30, SP=0, speed=2, armorType = armorTypes.VEHICLE, consumption=consumptionTypes.FUEL),
    "walker": armor("walker", AP=22, SP=0, speed=3, armorType = armorTypes.VEHICLE, consumption=consumptionTypes.FUEL),

    #Hover
    "helicopter": armor("helicopter", AP=8, SP=0, speed=10, armorType = armorTypes.HOVER, consumption=consumptionTypes.FUEL),

    #Naval
    "raft": armor("raft", AP=2, SP=0, speed=0, armorType = armorTypes.NAVAL, consumption=consumptionTypes.NONE),
    "sailBoat": armor("sailBoat", AP=15, SP=0, speed=0, armorType = armorTypes.NAVAL, consumption=consumptionTypes.NONE),
    "ironClad": armor("ironClad", AP=40, SP=0, speed=2, armorType = armorTypes.NAVAL, consumption=consumptionTypes.FUEL),
}