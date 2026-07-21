class Building:
    def __init__(self, x, y, sprite=None, tags=None):
        self.x = x
        self.y = y
        self.sprite = sprite
        self.tags = set(tags or [])
        self.active = True

    def update(self, game, dt):
        pass

    def on_removed(self, game):
        pass

    def add_resident(self, critter):
        pass

    def remove_resident(self, critter):
        pass

class Farm(Building):
    def __init__(self, x, y, sprite=None):
        super().__init__(x, y, sprite=sprite, tags={"food"})
        self.output = 2

class Harbor(Building):
    def __init__(self, x, y, sprite=None):
        super().__init__(x, y, sprite=sprite, tags={"port", "trade"})


class WolfDen(Building):
    SPAWN_COOLDOWN = 2.0

    def __init__(self, x, y, sprite=None, charges=0):
        super().__init__(x, y, sprite=sprite, tags={"den", "wolf"})
        self.charges = charges
        self.spawn_timer = 0.0
        self.resident_wolf_ids = set()

    @staticmethod
    def can_place_on_tile(tile):
        return tile is not None and tile.has_tag("land") and tile.terrain != "beach"

    def add_resident(self, critter):
        self.resident_wolf_ids.add(critter.id)

    def remove_resident(self, critter):
        self.resident_wolf_ids.discard(critter.id)

    def on_removed(self, game):
        for critter in game.critters:
            if getattr(critter, "home_building", None) is self:
                critter.home_building = None
        self.resident_wolf_ids.clear()

    def has_adjacent_deer(self, world):
        from critter import Deer

        for tile in world.get_neighbors_all(self.x, self.y):
            if tile.critter is not None and isinstance(tile.critter, Deer):
                return True
        return False

    def find_spawn_tile(self, world):
        origin_tile = world.get_tile(self.x, self.y)
        candidate_tiles = []
        if origin_tile is not None:
            candidate_tiles.append(origin_tile)
        candidate_tiles.extend(world.get_neighbors_all(self.x, self.y))

        for tile in candidate_tiles:
            if tile is None or tile.critter is not None or not tile.has_tag("land"):
                continue
            return tile

        return None

    def update(self, game, dt):
        from critter import Wolf
        from entity_cleanup import remove_building_at_tile

        tile = game.world.get_tile(self.x, self.y)
        if tile is None or tile.building is not self:
            return

        self.spawn_timer = max(0.0, self.spawn_timer - dt)

        if not WolfDen.can_place_on_tile(tile):
            remove_building_at_tile(game, tile, "its ground no longer supported a wolf den")
            return

        if self.charges > 0 and self.spawn_timer <= 0 and self.has_adjacent_deer(game.world):
            spawn_tile = self.find_spawn_tile(game.world)
            if spawn_tile is not None:
                wolf = Wolf(spawn_tile.x, spawn_tile.y)
                wolf.set_home_building(self)
                spawn_tile.critter = wolf
                game.critters.append(wolf)
                self.charges -= 1
                self.spawn_timer = self.SPAWN_COOLDOWN

        if self.charges <= 0 and not self.resident_wolf_ids:
            remove_building_at_tile(game, tile, "it had no wolves and no stored charges left")
