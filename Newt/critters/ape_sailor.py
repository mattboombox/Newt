from .ape import Ape


class ApeSailor(Ape):
    """A naval ape recruited to hunt valuable ocean prey."""

    ALLOWED_TERRAINS = {"ocean", "shallows", "beach"}
    HUNGER_INTERVAL = 260.0
    STARVATION_INTERVAL = 220.0
    MINIMUM_RETURN_HAUL = 3
    PREDATOR_NAME = "Ape Sailor"

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = (75, 115, 155)
        self.sprite = "ape_sailor"
        self.allowed_terrains = set(self.ALLOWED_TERRAINS)
        self.configure_hunger(self.HUNGER_INTERVAL, self.STARVATION_INTERVAL)

    @classmethod
    def recruit(cls, ape, world, x, y):
        if not Ape.is_recruitable_civilian(ape):
            return None

        old_tile = world.get_tile(ape.x, ape.y)
        if old_tile is not None and old_tile.critter is ape:
            old_tile.critter = None

        ape.__class__ = cls
        ape.x = x
        ape.y = y
        ape.color = (75, 115, 155)
        ape.sprite = "ape_sailor"
        ape.allowed_terrains = set(cls.ALLOWED_TERRAINS)
        ape.configure_hunger(cls.HUNGER_INTERVAL, cls.STARVATION_INTERVAL)
        ape.needs_habitat_relocation = False
        ape.set_behavior("recruited_sailor")
        world.get_tile(x, y).critter = ape
        return ape

    def get_hunt_prey_types(self):
        from .fish import Fish
        from .sea_scorpion import SeaScorpion
        from .squid import Squid
        from .whale import Whale

        return (Fish, SeaScorpion, Squid, Whale)

    def is_valid_hunt_prey(self, critter, prey_types):
        from .sperm_whale import SpermWhale

        return not isinstance(critter, SpermWhale) and super().is_valid_hunt_prey(
            critter,
            prey_types,
        )

    def is_valid_scavenge_prey(self, critter, prey_types):
        from .sperm_whale import SpermWhale

        return not isinstance(critter, SpermWhale) and super().is_valid_scavenge_prey(
            critter,
            prey_types,
        )

    def is_habitable_tile(self, tile):
        return super().is_habitable_tile(tile)

    def can_displace_critter(self, critter):
        # Sailors may clear lower-priority sea life from a coastal chokepoint,
        # but no longer shove equal- or higher-level residents.
        return self.DISPLACEMENT_LEVEL > critter.DISPLACEMENT_LEVEL

    def should_remove_on_failed_displacement(self, critter):
        # A shove that has nowhere to move its target should not become a kill.
        return False

    def is_at_connected_settlement_building(self, world, village):
        from building import NavalDistrict

        tile = world.get_tile(self.x, self.y)
        return (
            tile is not None
            and isinstance(tile.building, NavalDistrict)
            and tile.building.settlement is village
            and village.is_connected_building(world, tile.building)
        )

    def find_path_to_settlement(self, world, village):
        from building import NavalDistrict

        naval_districts = {
            building
            for building in village.get_connected_buildings(world)
            if isinstance(building, NavalDistrict)
        }
        return self.find_path_to_nearest_tile(
            world,
            lambda tile: tile.building in naval_districts,
            path_tile_predicate=self.can_path_through_tile,
        )

    def try_recover_stranded(self, game):
        world = game.world
        is_native_habitat = super().is_habitable_tile
        candidates = [
            tile
            for column in world.board
            for tile in column
            if (
                tile.critter is None
                and is_native_habitat(tile)
            )
        ]
        if not candidates:
            return False

        coastal_candidates = [
            tile
            for tile in candidates
            if tile.terrain in {"beach", "shallows"}
        ]
        if coastal_candidates:
            candidates = coastal_candidates

        destination = min(
            candidates,
            key=lambda tile: self.get_tile_distance(
                world,
                self.x,
                self.y,
                tile.x,
                tile.y,
            ),
        )
        old_tile = world.get_tile(self.x, self.y)
        if old_tile is not None and old_tile.critter is self:
            old_tile.critter = None

        self.x = destination.x
        self.y = destination.y
        destination.critter = self
        self.clear_settlement_path()
        self.set_behavior("return_to_shore")
        return True

    def create_offspring(self, x, y):
        return Ape(x, y)

    def try_reproduce(self, world):
        if self.is_reproduction_blocked(world):
            self.handle_blocked_reproduction()
            return None

        return self.try_spawn_adjacent_offspring(
            world,
            lambda tile: tile.terrain in Ape.ALLOWED_TERRAINS,
        )

    def try_handle_priority_behavior(self, game):
        return self.try_handle_hunter_priority_behavior(game)

    def should_return_carried_food(self):
        return self.carrying_food >= self.MINIMUM_RETURN_HAUL
