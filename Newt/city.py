from building import (
    Building,
    Farm,
    MilitaryDistrict,
    NavalDistrict,
    ResidentialDistrict,
    Ruins,
)


class City(Building):
    LEVEL_DATA = {
        "village": {"max_tags": 2, "population_cap": 5, "sprite_key": "village"},
        "town": {"max_tags": 4, "max_aux": 3, "population_cap": 200, "sprite_key": "town"},
        "city": {"max_tags": 6, "max_aux": 6, "population_cap": 1000, "sprite_key": "city"},
    }
    FARM_COST = 5
    MILITARY_DISTRICT_COST = 10
    MILITARY_DISTRICT_MIN_FOOD = 10
    MILITARY_DISTRICT_MIN_POPULATION = 10
    POPULATION_PER_MILITARY_DISTRICT = 25
    NAVAL_DISTRICT_COST = 5
    NAVAL_DISTRICT_MIN_FOOD = 5
    NAVAL_DISTRICT_MIN_POPULATION = 5
    POPULATION_PER_NAVAL_DISTRICT = 25
    RESIDENTIAL_COST = 5
    MIN_VILLAGE_DISTANCE = 12
    APES_PER_DOG = 4

    def __init__(
        self,
        x,
        y,
        level="village",
        population=0,
        world=None,
        sprite="ape_village",
        tags=None,
    ):
        settlement_tags = set(tags or ()) | {"settlement", level}
        super().__init__(x, y, sprite=sprite, tags=settlement_tags)
        self.level = level
        self.world = world
        self.food = 0
        self.resident_ape_ids = set()
        self.resident_dog_ids = set()
        self.aux_buildings = []
        self._connected_buildings_cache = None
        self._connected_buildings_world = None

    @property
    def population(self):
        return len(self.resident_ape_ids)

    @property
    def food_consumer_count(self):
        return self.population + len(self.resident_dog_ids)

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
        from critters.dog import Dog

        if isinstance(critter, Dog):
            self.resident_dog_ids.add(critter.id)
        else:
            self.resident_ape_ids.add(critter.id)

    def remove_resident(self, critter):
        self.resident_ape_ids.discard(critter.id)
        self.resident_dog_ids.discard(critter.id)

    def reconcile_residents(self, game):
        from critters.ape import Ape
        from critters.dog import Dog

        self.resident_ape_ids = {
            critter.id
            for critter in game.critters
            if (
                isinstance(critter, Ape)
                and not isinstance(critter, Dog)
                and critter.home_building is self
                and critter.current_behavior != "dying"
            )
        }
        self.resident_dog_ids = {
            critter.id
            for critter in game.critters
            if (
                isinstance(critter, Dog)
                and critter.home_building is self
                and critter.current_behavior != "dying"
            )
        }

    def update(self, game, dt):
        self.reconcile_residents(game)
        if self.population == 0:
            self.abandon_to_ruins(game)
            return

        self.ruin_disconnected_aux_buildings(game)
        self.try_expand_farms(game.world)
        self.try_expand_naval_districts(game.world)
        self.try_expand_military_districts(game.world)

    def replace_building_with_ruins(self, game, building):
        tile = game.world.get_tile(building.x, building.y)
        if tile is None or tile.building is not building:
            return False

        tile.building = Ruins(
            tile.x,
            tile.y,
            former_building_type=type(building).__name__,
        )
        building.settlement = None
        return True

    def ruin_disconnected_aux_buildings(self, game):
        connected_buildings = self.get_connected_buildings(game.world)
        disconnected_buildings = [
            building
            for building in self.aux_buildings
            if building not in connected_buildings
        ]
        for building in disconnected_buildings:
            self.replace_building_with_ruins(game, building)
            self.remove_aux_building(building)

    def abandon_to_ruins(self, game):
        for critter in game.critters:
            if getattr(critter, "home_building", None) is self:
                critter.home_building = None
        self.resident_ape_ids.clear()
        self.resident_dog_ids.clear()

        for building in [self, *self.aux_buildings]:
            self.replace_building_with_ruins(game, building)

        for building in self.aux_buildings:
            building.settlement = None
        self.aux_buildings.clear()
        self.invalidate_connected_buildings()

    def has_population_space(self):
        return self.population < self.population_cap

    @property
    def dog_capacity(self):
        return self.population // self.APES_PER_DOG

    def has_dog_space(self):
        return len(self.resident_dog_ids) < self.dog_capacity

    def add_aux_building(self, building):
        if building not in self.aux_buildings:
            self.aux_buildings.append(building)
            self.invalidate_connected_buildings()
        building.settlement = self

    def remove_aux_building(self, building):
        if building in self.aux_buildings:
            self.aux_buildings.remove(building)
            self.invalidate_connected_buildings()

    def invalidate_connected_buildings(self):
        self._connected_buildings_cache = None
        self._connected_buildings_world = None

    def get_connected_buildings(self, world):
        if (
            self._connected_buildings_cache is not None
            and self._connected_buildings_world is world
        ):
            return self._connected_buildings_cache

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

        self._connected_buildings_cache = frozenset(connected)
        self._connected_buildings_world = world
        return self._connected_buildings_cache

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
        if self.get_connected_farm_count(world) > 0:
            return None

        for tile in self.get_open_construction_tiles(world):
            if tile.terrain != "grass":
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

    def get_target_farm_count(self):
        food_consumers = self.food_consumer_count
        baseline = (food_consumers + 4) // 5
        food_shortage_buffer = int(food_consumers > 0 and self.food <= food_consumers)
        return baseline + food_shortage_buffer

    def should_prioritize_farms(self, world):
        return (
            self.get_connected_farm_count(world) < self.get_target_farm_count()
            and self.has_possible_connected_farm_site(world)
        )

    def try_expand_farms(self, world):
        if self.get_connected_farm_count(world) == 0:
            initial_farm = self.try_build_initial_farm(world)
            if initial_farm is not None:
                return initial_farm

        if not self.should_prioritize_farms(world):
            return None

        return self.try_build_farm(world)

    def get_connected_military_district_count(self, world):
        return sum(
            isinstance(building, MilitaryDistrict)
            for building in self.get_connected_buildings(world)
        )

    def try_build_military_district(self, world):
        if self.food < self.MILITARY_DISTRICT_MIN_FOOD:
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
        district = MilitaryDistrict(tile.x, tile.y, settlement=self)
        tile.building = district
        self.add_aux_building(district)
        self.food -= self.MILITARY_DISTRICT_COST
        return district

    def try_expand_military_districts(self, world):
        if self.population < self.MILITARY_DISTRICT_MIN_POPULATION:
            return None

        target_district_count = max(
            1,
            (self.population + self.POPULATION_PER_MILITARY_DISTRICT - 1)
            // self.POPULATION_PER_MILITARY_DISTRICT,
        )
        if self.get_connected_military_district_count(world) >= target_district_count:
            return None

        return self.try_build_military_district(world)

    def get_connected_naval_district_count(self, world):
        return sum(
            isinstance(building, NavalDistrict)
            for building in self.get_connected_buildings(world)
        )

    def has_possible_connected_farm_site(self, world):
        return any(
            tile.terrain == "grass"
            for tile in self.get_open_construction_tiles(world)
        )

    def try_build_naval_district(self, world):
        if self.food < self.NAVAL_DISTRICT_MIN_FOOD:
            return None

        candidates = [
            tile
            for tile in self.get_open_construction_tiles(world)
            if tile.terrain == "shallows"
        ]
        if not candidates:
            return None

        tile = candidates[0]
        district = NavalDistrict(tile.x, tile.y, settlement=self)
        tile.building = district
        self.add_aux_building(district)
        self.food -= self.NAVAL_DISTRICT_COST
        return district

    def try_expand_naval_districts(self, world):
        has_no_farm_option = (
            self.get_connected_farm_count(world) == 0
            and not self.has_possible_connected_farm_site(world)
        )
        if (
            self.population < self.NAVAL_DISTRICT_MIN_POPULATION
            and not has_no_farm_option
        ):
            return None

        target_district_count = max(
            1,
            (self.population + self.POPULATION_PER_NAVAL_DISTRICT - 1)
            // self.POPULATION_PER_NAVAL_DISTRICT,
        )
        if self.get_connected_naval_district_count(world) >= target_district_count:
            return None

        return self.try_build_naval_district(world)

    def try_build_residential_district(self, world):
        if self.should_prioritize_farms(world) or self.food < self.RESIDENTIAL_COST:
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
        self.resident_dog_ids.clear()

        for building in self.aux_buildings:
            self.replace_building_with_ruins(game, building)
            building.settlement = None
        self.aux_buildings.clear()
        self.invalidate_connected_buildings()
