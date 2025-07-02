class Unit:
    def __init__(self, name, hp, dmg, color):
        self.name = name
        self.hp = hp
        self.dmg = dmg
        self.color = color

unitLib = {
    "warrior": Unit("warrior", hp=5, dmg=2, color=(255, 0, 0)),
    "spearmen": Unit("spearmen", hp=5, dmg=3, color=(200, 0, 0)),
}
       
