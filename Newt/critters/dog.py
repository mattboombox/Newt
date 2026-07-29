from .ape_warrior import ApeWarrior


class Dog(ApeWarrior):
    """A domesticated wolf that lives and fights alongside village apes."""

    PREDATOR_NAME = "Dog"
    FERAL_INTERVAL = 60.0

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = (170, 125, 80)
        self.sprite = "dog"
        self.feral_timer = self.FERAL_INTERVAL

    @classmethod
    def tame(cls, wolf, village):
        from .wolf import Wolf

        if (
            not isinstance(wolf, Wolf)
            or village is None
            or not village.has_dog_space()
        ):
            return None

        wolf.clear_home_building()
        wolf.__class__ = cls
        wolf.color = (170, 125, 80)
        wolf.sprite = "dog"
        wolf.allowed_terrains = set(cls.ALLOWED_TERRAINS)
        wolf.move_cooldown = 0.28
        wolf.carrying_food = 0
        wolf.carrying_den_charge = False
        wolf.feral_timer = cls.FERAL_INTERVAL
        wolf.meals_eaten = 0
        wolf.needs_habitat_relocation = False
        wolf.configure_hunger(cls.HUNGER_INTERVAL, cls.STARVATION_INTERVAL)
        wolf.set_behavior("tamed")
        wolf.set_home_building(village)
        return wolf

    def create_offspring(self, x, y):
        return Dog(x, y)

    def find_accessible_village(self, world):
        from city import City

        path = self.find_path_to_nearest_tile(
            world,
            lambda tile: (
                isinstance(tile.building, City)
                and tile.building.has_population_space()
                and tile.building.has_dog_space()
            ),
            allow_occupied_target=True,
            max_search_distance=self.VILLAGE_CLAIM_RANGE,
            path_tile_predicate=self.is_habitable_tile,
        )
        if path is None:
            return None

        tile = world.get_tile(self.x, self.y) if not path else world.get_tile(*path[-1])
        if tile is None or not isinstance(tile.building, City):
            return None
        return tile.building

    def ensure_home_village(self, game):
        village = self.get_home_village(game.world)
        if village is not None:
            self.feral_timer = self.FERAL_INTERVAL
            return village

        village = self.find_accessible_village(game.world)
        if (
            village is not None
            and village.has_population_space()
            and village.has_dog_space()
        ):
            self.set_home_building(village)
            self.feral_timer = self.FERAL_INTERVAL
            return village
        return None

    def try_reproduce_in_village(self, game, village):
        if not village.has_dog_space():
            return self.defer_reproduction("await_dog_capacity")

        return super().try_reproduce_in_village(game, village)

    def update(self, game, dt):
        if self.current_behavior == "dying":
            super().update(game, dt)
            return

        if self.get_home_village(game.world) is not None:
            self.feral_timer = self.FERAL_INTERVAL
        else:
            self.feral_timer -= dt
            if self.feral_timer <= 0:
                self.become_feral()
                return

        super().update(game, dt)

    def become_feral(self):
        from .wolf import Wolf

        self.clear_home_building()
        self.__class__ = Wolf
        self.color = (160, 160, 160)
        self.sprite = "wolf"
        self.allowed_terrains = set(Wolf.ALLOWED_TERRAINS)
        self.move_cooldown = 0.32
        self.carrying_food = 0
        self.carrying_den_charge = False
        self.meals_eaten = 0
        self.needs_habitat_relocation = False
        self.configure_hunger(Wolf.HUNGER_INTERVAL, Wolf.STARVATION_INTERVAL)
        self.set_behavior("feral")
        return self

    def get_reproduction_blocking_types(self):
        return (Dog,)
