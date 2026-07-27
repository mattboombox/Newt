from .ape import Ape


class ApeWarrior(Ape):
    """A village defender recruited to hunt threats to ape settlements."""

    PREDATOR_NAME = "Ape Warrior"

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = (145, 85, 65)
        self.sprite = "ape_warrior"

    @classmethod
    def recruit(cls, ape):
        ape.__class__ = cls
        ape.color = (145, 85, 65)
        ape.sprite = "ape_warrior"
        ape.set_behavior("recruited")
        return ape

    def get_hunt_prey_types(self):
        from .land_kraken import LandKraken
        from .mega_spider import MegaSpider
        from .wolf import Wolf

        return (LandKraken, MegaSpider, Wolf)

    def get_scavenge_prey_types(self):
        if self.carrying_food:
            return ()
        return self.get_hunt_prey_types()

    def try_reproduce_in_village(self, game, village):
        # Military districts recruit warriors from the civilian population;
        # warriors do not create a separate reproductive lineage.
        self.meals_eaten = 0
        self.set_behavior("patrol")
        return False
