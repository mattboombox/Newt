from building import Building, Farm, ResidentialDistrict, Ruins


class City(Building):
    LEVEL_DATA = {
        "village": {"max_tags": 2, "population_cap": 5, "sprite_key": "village"},
        "town": {"max_tags": 4, "max_aux": 3, "population_cap": 200, "sprite_key": "town"},
        "city": {"max_tags": 6, "max_aux": 6, "population_cap": 1000, "sprite_key": "city"},
    }
    FARM_COST = 5
    RESIDENTIAL_COST = 5
    MIN_VILLAGE_DISTANCE = 12

    def __init__(self, x, y, level="village", population=0, world=None, sprite=None, tags=None):
        settlement_tags = set(tags or ()) | {"settlement", level}
        super().__init__(x, y, sprite=sprite, tags=settlement_tags)
        self.level = level
        self.world = world
        self.food = 0
        self.resident_ape_ids = set()
        self.aux_buildings = []

    @property
    def population(self):
        return len(self.resident_ape_ids)

    @property
    def population_cap(self):
        base_cap = self.LEVEL_DATA[self.level]["population_cap"]
        if self.world is None:
            connected_buildings = set(self.aux_buildings)
        else:
            connected_buildings = self.get_connected_buildings(self.world)
        residential_cap = sum(
            building.POPULATION_CAPACITY
            for building in connected_buildings
            if isinstance(building, ResidentialDistrict)
        )
        return base_cap + residential_cap

    @classmethod
    def get_villages(cls, world):
        return [
            tile.building
            for column in world.board
            for tile in column
            if isinstance(tile.building, City)
        ]

    @classmethod
    def is_far_enough_from_other_villages(cls, world, x, y, villages=None):
        if villages is None:
            villages = cls.get_villages(world)

        for village in villages:
            dx = abs(x - village.x)
            dx = min(dx, world.cols - dx)
            dy = abs(y - village.y)
            if dx + dy <= cls.MIN_VILLAGE_DISTANCE:
                return False
        return True

    @classmethod
    def can_place_on_tile(cls, tile, require_farm_site=False, villages=None):
        if tile is None or tile.building is not None or not tile.has_tag("land"):
            return False

        if not cls.is_far_enough_from_other_villages(
            tile.world,
            tile.x,
            tile.y,
            villages=villages,
        ):
            return False

        if not require_farm_site:
            return True

        return any(
            neighbor.building is None
            and neighbor.critter is None
            and neighbor.terrain == "grass"
            for neighbor in tile.world.get_neighbors_cardinal(tile.x, tile.y)
        )

    def add_resident(self, critter):
        self.resident_ape_ids.add(critter.id)

    def remove_resident(self, critter):
        self.resident_ape_ids.discard(critter.id)

    def reconcile_residents(self, game):
        from critters.ape import Ape

        self.resident_ape_ids = {
            critter.id
            for critter in game.critters
            if (
                isinstance(critter, Ape)
                and critter.home_building is self
                and critter.current_behavior != "dying"
            )
        }

    def update(self, game, dt):
        self.reconcile_residents(game)
        if self.population == 0:
            self.abandon_to_ruins(game)
            return

        self.try_expand_farms(game.world)

    def abandon_to_ruins(self, game):
        connected_buildings = self.get_connected_buildings(game.world)

        for critter in game.critters:
            if getattr(critter, "home_building", None) is self:
                critter.home_building = None
        self.resident_ape_ids.clear()

        for building in connected_buildings:
            tile = game.world.get_tile(building.x, building.y)
            if tile is None or tile.building is not building:
                continue

            tile.building = Ruins(
                tile.x,
                tile.y,
                former_building_type=type(building).__name__,
            )

        for building in self.aux_buildings:
            building.settlement = None
        self.aux_buildings.clear()

    def has_population_space(self):
        return self.population < self.population_cap

    def add_aux_building(self, building):
        if building not in self.aux_buildings:
            self.aux_buildings.append(building)
        building.settlement = self

    def remove_aux_building(self, building):
        if building in self.aux_buildings:
            self.aux_buildings.remove(building)

    def get_connected_buildings(self, world):
        connected = {self}
        frontier = [self]
        owned_buildings = set(self.aux_buildings)

        while frontier:
            building = frontier.pop()
            for tile in world.get_neighbors_cardinal(building.x, building.y):
                neighbor = tile.building
                if neighbor in owned_buildings and neighbor not in connected:
                    connected.add(neighbor)
                    frontier.append(neighbor)

        return connected

    def is_connected_building(self, world, building):
        return building in self.get_connected_buildings(world)

    @classmethod
    def find_connectable_village(cls, world, tile):
        villages = set()
        for neighbor_tile in world.get_neighbors_cardinal(tile.x, tile.y):
            building = neighbor_tile.building
            if isinstance(building, City):
                villages.add(building)
                continue

            village = getattr(building, "settlement", None)
            if (
                isinstance(village, City)
                and village.is_connected_building(world, building)
            ):
                villages.add(village)

        if len(villages) != 1:
            return None
        return next(iter(villages))

    @classmethod
    def connect_aux_building(cls, world, building):
        tile = world.get_tile(building.x, building.y)
        if tile is None:
            return None

        village = cls.find_connectable_village(world, tile)
        if village is None:
            return None

        village.add_aux_building(building)
        return village

    def get_open_construction_tiles(self, world):
        candidates = []
        seen_positions = set()
        for building in self.get_connected_buildings(world):
            for tile in world.get_neighbors_cardinal(building.x, building.y):
                position = (tile.x, tile.y)
                if (
                    position in seen_positions
                    or tile.building is not None
                    or tile.critter is not None
                ):
                    continue
                seen_positions.add(position)
                candidates.append(tile)
        return candidates

    def try_build_initial_farm(self, world):
        if any(isinstance(building, Farm) for building in self.aux_buildings):
            return None

        for tile in world.get_neighbors_cardinal(self.x, self.y):
            if tile.building is not None or tile.critter is not None or tile.terrain != "grass":
                continue

            farm = Farm(tile.x, tile.y, settlement=self)
            tile.building = farm
            self.add_aux_building(farm)
            return farm

        return None

    def get_connected_farm_count(self, world):
        return sum(
            isinstance(building, Farm)
            for building in self.get_connected_buildings(world)
        )

    def try_build_farm(self, world):
        if self.food < self.FARM_COST:
            return None

        candidates = [
            tile
            for tile in self.get_open_construction_tiles(world)
            if tile.terrain == "grass"
        ]
        if not candidates:
            return None

        tile = candidates[0]
        farm = Farm(tile.x, tile.y, settlement=self)
        tile.building = farm
        self.add_aux_building(farm)
        self.food -= self.FARM_COST
        return farm

    def try_expand_farms(self, world):
        target_farm_count = (self.population + 4) // 5
        if self.get_connected_farm_count(world) >= target_farm_count:
            return None

        return self.try_build_farm(world)

    def try_build_residential_district(self, world):
        if self.food < self.RESIDENTIAL_COST:
            return None

        candidates = [
            tile
            for tile in self.get_open_construction_tiles(world)
            if tile.has_tag("land")
        ]
        candidates.sort(key=lambda tile: tile.terrain == "grass")
        if not candidates:
            return None

        tile = candidates[0]
        district = ResidentialDistrict(tile.x, tile.y, settlement=self)
        tile.building = district
        self.add_aux_building(district)
        self.food -= self.RESIDENTIAL_COST
        return district

    def on_removed(self, game):
        for critter in game.critters:
            if getattr(critter, "home_building", None) is self:
                critter.home_building = None
        self.resident_ape_ids.clear()

        for building in self.aux_buildings:
            building.settlement = None
        self.aux_buildings.clear()
