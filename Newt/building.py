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

    def can_remain_on_tile(self, tile):
        settlement = getattr(self, "settlement", None)
        if (
            settlement is not None
            and getattr(settlement, "faction", None) == "merfolk"
        ):
            return settlement.is_valid_auxiliary_tile(tile)
        return tile is not None and not tile.has_tag("water")

class Farm(Building):
    FOOD_INTERVAL = 14

    def __init__(self, x, y, settlement=None, sprite="ape_farm"):
        super().__init__(x, y, sprite=sprite, tags={"food"})
        self.settlement = settlement
        self.output = 1
        self.food_timer = self.FOOD_INTERVAL

    def update(self, game, dt):
        if self.settlement is None or not self.settlement.is_connected_building(game.world, self):
            return

        self.food_timer -= dt
        while self.food_timer <= 0:
            self.food_timer += self.FOOD_INTERVAL
            self.settlement.food += self.output

    def on_removed(self, game):
        if self.settlement is not None:
            self.settlement.remove_aux_building(self)
            self.settlement = None


class ResidentialDistrict(Building):
    POPULATION_CAPACITY = 5

    def __init__(self, x, y, settlement=None, sprite="ape_hut"):
        super().__init__(x, y, sprite=sprite, tags={"residential"})
        self.settlement = settlement

    def on_removed(self, game):
        if self.settlement is not None:
            self.settlement.remove_aux_building(self)
            self.settlement = None


class Church(Building):
    RECRUITMENT_INTERVAL = 30.0

    def __init__(self, x, y, settlement=None, sprite="church"):
        super().__init__(x, y, sprite=sprite, tags={"religious"})
        self.settlement = settlement
        self.recruitment_timer = self.RECRUITMENT_INTERVAL
        self.has_recruited = False

    def try_recruit(self, game):
        from critters.ape import Ape
        from critters.messiah import Messiah

        if self.has_recruited or self.settlement is None:
            return None

        civilians = [
            critter
            for critter in game.critters
            if (
                Ape.is_recruitable_civilian(critter)
                and critter.home_building is self.settlement
                and critter.current_behavior != "dying"
            )
        ]
        if len(civilians) <= 1:
            return None

        recruit = min(
            civilians,
            key=lambda ape: abs(ape.x - self.x) + abs(ape.y - self.y),
        )
        messiah = Messiah.recruit(recruit)
        if messiah is not None:
            self.has_recruited = True
        return messiah

    def update(self, game, dt):
        if (
            self.has_recruited
            or self.settlement is None
            or not self.settlement.is_connected_building(game.world, self)
        ):
            return

        self.recruitment_timer -= dt
        if self.recruitment_timer > 0:
            return

        self.recruitment_timer += self.RECRUITMENT_INTERVAL
        self.try_recruit(game)

    def on_removed(self, game):
        if self.settlement is not None:
            self.settlement.remove_aux_building(self)
            self.settlement = None


class MilitaryDistrict(Building):
    RECRUITMENT_COST = 0
    RECRUITMENT_INTERVAL = 30.0
    WARRIOR_CAPACITY = 5

    def __init__(self, x, y, settlement=None, sprite="ape_fort"):
        super().__init__(x, y, sprite=sprite, tags={"military"})
        self.settlement = settlement
        self.recruitment_timer = self.RECRUITMENT_INTERVAL

    def get_village_warriors(self, game):
        from critters.ape_warrior import ApeWarrior

        return [
            critter
            for critter in game.critters
            if (
                isinstance(critter, ApeWarrior)
                and critter.home_building is self.settlement
                and critter.current_behavior != "dying"
            )
        ]

    def get_connected_military_capacity(self, world):
        if self.settlement is None:
            return 0

        return sum(
            building.WARRIOR_CAPACITY
            for building in self.settlement.get_connected_buildings(world)
            if isinstance(building, MilitaryDistrict)
        )

    def try_recruit(self, game):
        village = self.settlement
        if village is None:
            return None

        if len(self.get_village_warriors(game)) >= self.get_connected_military_capacity(game.world):
            return None

        civilians = [
            critter
            for critter in game.critters
            if (
                village.is_recruitable_civilian(critter)
                and critter.home_building is village
                and critter.current_behavior != "dying"
            )
        ]
        if len(civilians) <= 1:
            return None

        recruit = min(
            civilians,
            key=lambda ape: abs(ape.x - self.x) + abs(ape.y - self.y),
        )
        return village.get_warrior_type().recruit(recruit)

    def update(self, game, dt):
        if self.settlement is None or not self.settlement.is_connected_building(game.world, self):
            return

        self.recruitment_timer -= dt
        if self.recruitment_timer > 0:
            return

        self.recruitment_timer += self.RECRUITMENT_INTERVAL
        self.try_recruit(game)

    def on_removed(self, game):
        if self.settlement is not None:
            self.settlement.remove_aux_building(self)
            self.settlement = None


class NavalDistrict(Building):
    RECRUITMENT_COST = 0
    RECRUITMENT_INTERVAL = 30.0
    SAILOR_CAPACITY = 4

    def __init__(self, x, y, settlement=None, sprite="ape_harbor"):
        super().__init__(x, y, sprite=sprite, tags={"naval"})
        self.settlement = settlement
        self.recruitment_timer = self.RECRUITMENT_INTERVAL

    def can_remain_on_tile(self, tile):
        return tile is not None and tile.has_tag("water")

    def get_village_sailors(self, game):
        from critters.ape_sailor import ApeSailor

        return [
            critter
            for critter in game.critters
            if (
                isinstance(critter, ApeSailor)
                and critter.home_building is self.settlement
                and critter.current_behavior != "dying"
            )
        ]

    def get_connected_naval_capacity(self, world):
        if self.settlement is None:
            return 0

        return sum(
            building.SAILOR_CAPACITY
            for building in self.settlement.get_connected_buildings(world)
            if isinstance(building, NavalDistrict)
        )

    def try_recruit(self, game):
        from critters.ape import Ape
        from critters.ape_sailor import ApeSailor

        village = self.settlement
        tile = game.world.get_tile(self.x, self.y)
        if (
            village is None
            or tile is None
            or tile.building is not self
            or tile.critter is not None
        ):
            return None

        if len(self.get_village_sailors(game)) >= self.get_connected_naval_capacity(game.world):
            return None

        civilians = [
            critter
            for critter in game.critters
            if (
                Ape.is_recruitable_civilian(critter)
                and critter.home_building is village
                and critter.current_behavior != "dying"
            )
        ]
        if len(civilians) <= 1:
            return None

        recruit = min(
            civilians,
            key=lambda ape: abs(ape.x - self.x) + abs(ape.y - self.y),
        )
        return ApeSailor.recruit(
            recruit,
            game.world,
            self.x,
            self.y,
        )

    def update(self, game, dt):
        if self.settlement is None or not self.settlement.is_connected_building(game.world, self):
            return

        self.recruitment_timer -= dt
        if self.recruitment_timer > 0:
            return

        self.recruitment_timer += self.RECRUITMENT_INTERVAL
        self.try_recruit(game)

    def on_removed(self, game):
        if self.settlement is not None:
            self.settlement.remove_aux_building(self)
            self.settlement = None


class Ruins(Building):
    def __init__(self, x, y, former_building_type=None, sprite=None):
        if sprite is None:
            sprite = f"ruin_{random.randint(1, 6)}"
        super().__init__(x, y, sprite=sprite, tags={"ruins"})
        self.former_building_type = former_building_type


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
        from critters import CRITTER_TYPES
        from entity_cleanup import remove_critter

        spawn_tiles = self.get_open_spawn_tiles(game.world)
        if not spawn_tiles:
            return False

        printable_critters = tuple(
            (name, critter_cls)
            for name, critter_cls in CRITTER_TYPES.items()
            if not getattr(critter_cls, "PLAYER_SPAWN_ONLY", False)
        )
        critter_name, critter_cls = random.choice(printable_critters)
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
    PREFERRED_VILLAGE_GAP = 2

    def __init__(self, x, y, sprite="den", charges=0):
        super().__init__(x, y, sprite=sprite, tags={"den", "wolf"})
        self.charges = charges
        self.spawn_timer = 0.0
        self.resident_wolf_ids = set()

    @staticmethod
    def can_place_on_tile(tile):
        return tile is not None and tile.has_tag("land") and tile.terrain != "beach"

    @classmethod
    def has_preferred_village_clearance(cls, world, x, y):
        from city import City

        gap = cls.PREFERRED_VILLAGE_GAP
        for dy in range(-gap, gap + 1):
            ny = y + dy
            if ny < 0 or ny >= world.rows:
                continue

            max_dx = gap - abs(dy)
            for dx in range(-max_dx, max_dx + 1):
                nx = (x + dx) % world.cols
                building = world.get_tile(nx, ny).building
                if isinstance(building, City):
                    return False

                if isinstance(getattr(building, "settlement", None), City):
                    return False

        return True

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
        from critters import Wolf

        prey_selector = Wolf.HUNT_PREY_RULE

        for tile in world.get_neighbors_all(self.x, self.y):
            if (
                tile.critter is not None
                and Wolf.matches_prey_selector(tile.critter, prey_selector)
            ):
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
        from critters import Wolf
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
    def __init__(self, x, y, world=None, charges=0, sprite="web"):
        super().__init__(x, y, sprite=sprite, tags={"web", "spider"})
        self.world = world
        self.charges = charges
        self.resident_spider_ids = set()

    @staticmethod
    def can_place_on_tile(tile):
        return tile is not None and tile.has_tag("land") and tile.terrain != "beach"

    def add_resident(self, critter):
        self.resident_spider_ids.add(critter.id)

    def update(self, game, dt):
        from critters import MegaSpider
        from entity_cleanup import remove_building_at_tile

        owners = sorted(
            (
                critter
                for critter in game.critters
                if (
                    isinstance(critter, MegaSpider)
                    and critter.current_behavior != "dying"
                    and getattr(critter, "home_building", None) is self
                )
            ),
            key=lambda critter: critter.id,
        )

        if not owners:
            tile = game.world.get_tile(self.x, self.y)
            if tile is not None and tile.building is self:
                remove_building_at_tile(
                    game,
                    tile,
                    "it no longer had a living Mega Spider owner",
                )
            return

        owner = owners[0]
        for extra_owner in owners[1:]:
            extra_owner.home_building = None
        self.resident_spider_ids = {owner.id}

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
        from critters import MegaSpider

        if isinstance(critter, MegaSpider):
            return

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
