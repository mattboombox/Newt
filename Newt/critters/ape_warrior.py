from .ape import Ape


class ApeWarrior(Ape):
    """A village defender recruited to hunt threats to ape settlements."""

    PREDATOR_NAME = "Ape Warrior"
    STARVATION_INTERVAL = 120.0

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = (145, 85, 65)
        self.sprite = "ape_warrior"
        self.configure_hunger(self.HUNGER_INTERVAL, self.STARVATION_INTERVAL)

    @classmethod
    def recruit(cls, ape):
        if not Ape.is_recruitable_civilian(ape):
            return None

        ape.__class__ = cls
        ape.color = (145, 85, 65)
        ape.sprite = "ape_warrior"
        ape.configure_hunger(cls.HUNGER_INTERVAL, cls.STARVATION_INTERVAL)
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

    def create_offspring(self, x, y):
        return Ape(x, y)

    def try_handle_priority_behavior(self, game):
        return self.try_handle_hunter_priority_behavior(game)
