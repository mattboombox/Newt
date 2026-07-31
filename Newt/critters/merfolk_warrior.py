from .ape_warrior import ApeWarrior
from .merfolk import Merfolk


class MerfolkWarrior(ApeWarrior):
    """A military defender recruited by a Merfolk settlement."""

    ALLOWED_TERRAINS = Merfolk.ALLOWED_TERRAINS
    VILLAGE_FACTION = Merfolk.VILLAGE_FACTION
    PREDATOR_NAME = "Merfolk Warrior"

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = (55, 115, 150)
        self.sprite = "merfolk_warrior"
        self.allowed_terrains = set(self.ALLOWED_TERRAINS)

    @classmethod
    def recruit(cls, merfolk):
        if not Merfolk.is_recruitable_civilian(merfolk):
            return None

        merfolk.__class__ = cls
        merfolk.color = (55, 115, 150)
        merfolk.sprite = "merfolk_warrior"
        merfolk.allowed_terrains = set(cls.ALLOWED_TERRAINS)
        merfolk.configure_combat()
        merfolk.configure_hunger(cls.HUNGER_INTERVAL, cls.STARVATION_INTERVAL)
        merfolk.deer_taming_contact_ids = set()
        merfolk.set_behavior("recruited")
        return merfolk

    def create_offspring(self, x, y):
        return Merfolk(x, y)

    def try_tame_adjacent_deer(self, game):
        return False

