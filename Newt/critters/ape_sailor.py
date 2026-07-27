from .ape import Ape


class ApeSailor(Ape):
    """A naval ape recruited to hunt valuable ocean prey."""

    ALLOWED_TERRAINS = {"ocean", "shallows"}
    PREDATOR_NAME = "Ape Sailor"

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = (75, 115, 155)
        self.sprite = "ape_sailor"
        self.allowed_terrains = set(self.ALLOWED_TERRAINS)

    @classmethod
    def recruit(cls, ape, world, x, y):
        old_tile = world.get_tile(ape.x, ape.y)
        if old_tile is not None and old_tile.critter is ape:
            old_tile.critter = None

        ape.__class__ = cls
        ape.x = x
        ape.y = y
        ape.color = (75, 115, 155)
        ape.sprite = "ape_sailor"
        ape.allowed_terrains = set(cls.ALLOWED_TERRAINS)
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
