from .ape import Ape


class Merfolk(Ape):
    CRITTER_TAGS = frozenset({"animal", "aquatic", "vertebrate", "sapient"})
    """An aquatic civilization-builder with ape-like settlement behavior."""

    ALLOWED_TERRAINS = {"ocean", "trench", "shallows", "beach"}
    VILLAGE_FACTION = "merfolk"
    PREDATOR_NAME = "Merfolk"

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = (70, 155, 175)
        self.sprite = "merfolk"
        self.allowed_terrains = set(self.ALLOWED_TERRAINS)

    @staticmethod
    def is_recruitable_civilian(critter):
        return type(critter) is Merfolk

    def get_hunt_prey_types(self):
        from .crab import Crab
        from .fish import Fish
        from .jelly_fish import Jellyfish
        from .nautilus import Nautilus
        from .sea_scorpion import SeaScorpion
        from .squid import Squid
        from .trilobite import Trilobite

        return (
            Crab,
            Fish,
            Jellyfish,
            Nautilus,
            SeaScorpion,
            Squid,
            Trilobite,
        )

    def try_tame_adjacent_wolf(self, game):
        return False
