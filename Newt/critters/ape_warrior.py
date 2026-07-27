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

    def create_offspring(self, x, y):
        return Ape(x, y)

    def try_handle_priority_behavior(self, game):
        if self.carrying_food:
            return super().try_handle_priority_behavior(game)

        if self.hunt_nearest_prey(
            game,
            self.get_hunt_prey_types(),
            self.get_predator_name(),
        ):
            return True

        return super().try_handle_priority_behavior(game)
