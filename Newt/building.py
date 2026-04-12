class Building:
    def __init__(self, x, y, sprite=None, tags=None):
        self.x = x
        self.y = y
        self.sprite = sprite
        self.tags = set(tags or [])
        self.active = True

    def update(self, world, dt):
        pass

class Farm(Building):
    def __init__(self, x, y, sprite=None):
        super().__init__(x, y, sprite=sprite, tags={"food"})
        self.output = 2

class Harbor(Building):
    def __init__(self, x, y, sprite=None):
        super().__init__(x, y, sprite=sprite, tags={"port", "trade"})