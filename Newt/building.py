import random


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


class CritterPrinter(Building):
    MIN_SPAWN_INTERVAL = 8.0
    MAX_SPAWN_INTERVAL = 16.0

    def __init__(self, x, y, sprite=None):
        super().__init__(x, y, sprite=sprite, tags={"alien", "printer"})
        self.spawn_timer = self.get_next_spawn_interval()
        self.printed_count = 0
        self.last_printed_critter = None

    @classmethod
    def get_next_spawn_interval(cls):
        return random.uniform(cls.MIN_SPAWN_INTERVAL, cls.MAX_SPAWN_INTERVAL)

    def get_open_spawn_tiles(self, world):
        candidates = []
        origin_tile = world.get_tile(self.x, self.y)
        if origin_tile is not None:
            candidates.append(origin_tile)
        candidates.extend(world.get_neighbors_all(self.x, self.y))
        return [tile for tile in candidates if tile.critter is None]

    def try_print_critter(self, game):
        from critter import CRITTER_TYPES
        from entity_cleanup import remove_critter

        spawn_tiles = self.get_open_spawn_tiles(game.world)
        if not spawn_tiles:
            return False

        critter_name, critter_cls = random.choice(tuple(CRITTER_TYPES.items()))
        random.shuffle(spawn_tiles)

        for spawn_tile in spawn_tiles:
            critter = critter_cls(spawn_tile.x, spawn_tile.y)
            # The printer deliberately ignores habitat. Base critters can
            # cross incompatible terrain while seeking somewhere survivable.
            critter.needs_habitat_relocation = not critter.is_habitable_tile(spawn_tile)
            spawn_tile.critter = critter
            game.critters.append(critter)

            on_spawn = getattr(critter, "on_spawn", None)
            if on_spawn is not None and not on_spawn(game, allow_incompatible=True):
                remove_critter(game, critter, "the printer could not assemble its full body")
                continue

            self.printed_count += 1
            self.last_printed_critter = critter_name
            print(
                f"Critter Printer at ({self.x}, {self.y}) printed "
                f"{critter_name} {critter.id} on {spawn_tile.terrain}."
            )
            return True

        return False

    def update(self, game, dt):
        tile = game.world.get_tile(self.x, self.y)
        if tile is None or tile.building is not self:
            return

        self.spawn_timer -= dt
        if self.spawn_timer > 0:
            return

        self.try_print_critter(game)
        self.spawn_timer = self.get_next_spawn_interval()


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

    def has_adjacent_wolf_prey(self, world):
        from critter import Wolf

        prey_types = Wolf.HUNT_PREY_TYPES

        for tile in world.get_neighbors_all(self.x, self.y):
            if tile.critter is not None and isinstance(tile.critter, prey_types):
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

        if (
            self.charges > 0
            and self.spawn_timer <= 0
            and self.has_adjacent_wolf_prey(game.world)
        ):
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


class SpiderWeb(Building):
    def __init__(self, x, y, world=None, charges=0):
        super().__init__(x, y, tags={"web", "spider"})
        self.world = world
        self.charges = charges
        self.resident_spider_ids = set()

    @staticmethod
    def can_place_on_tile(tile):
        return tile is not None and tile.has_tag("land") and tile.terrain != "beach"

    def add_resident(self, critter):
        self.resident_spider_ids.add(critter.id)

    def remove_resident(self, critter):
        self.resident_spider_ids.discard(critter.id)
        if self.resident_spider_ids or self.world is None:
            return

        tile = self.world.get_tile(self.x, self.y)
        if tile is None or tile.building is not self:
            return

        if tile.critter is not None and getattr(tile.critter, "trapped_by_web", None) is self:
            tile.critter.trapped_by_web = None
        tile.building = None

    def trap_critter(self, critter):
        if critter.id in self.resident_spider_ids or getattr(critter, "home_building", None) is self:
            return

        critter.trapped_by_web = self
        critter.set_behavior("trapped")

    def has_trapped_prey(self, world):
        tile = world.get_tile(self.x, self.y)
        return (
            tile is not None
            and tile.critter is not None
            and getattr(tile.critter, "trapped_by_web", None) is self
        )

    def consume_trapped_prey(self, game, spider):
        from entity_cleanup import remove_critter

        tile = game.world.get_tile(self.x, self.y)
        prey = None if tile is None else tile.critter
        if prey is None or getattr(prey, "trapped_by_web", None) is not self:
            return False

        remove_critter(game, prey, f"it was caught in a web by Mega Spider {spider.id}")
        # Keep a modest emergency reserve.  Once it is full, fresh trapped
        # prey becomes an actual meal so the spider can reproduce rather
        # than stockpiling an unlimited number of corpses.
        if spider.is_hungry or self.charges >= spider.WEB_RESERVE_CAP:
            spider.handle_successful_meal(game)
        else:
            self.charges += 1
            spider.set_behavior("store_food")
        return True

    def on_removed(self, game):
        for critter in game.critters:
            if getattr(critter, "home_building", None) is self:
                critter.home_building = None
            if getattr(critter, "trapped_by_web", None) is self:
                critter.trapped_by_web = None
        self.resident_spider_ids.clear()
