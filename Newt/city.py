from building import Building

class City(Building):
    LEVEL_DATA = {
        "village": {"max_tags": 2, "max_aux": 1, "population_cap": 50, "sprite_key": "village"},
        "town": {"max_tags": 4, "max_aux": 3, "population_cap": 200, "sprite_key": "town"},
        "city": {"max_tags": 6, "max_aux": 6, "population_cap": 1000, "sprite_key": "city"},
    }

    def __init__(self, x, y, level="village", population=10, sprite=None, tags=None):
        super().__init__(x, y, sprite=sprite, tags=tags)
        self.level = level
        self.population = population
        self.aux_buildings = []   # references to other Building objects