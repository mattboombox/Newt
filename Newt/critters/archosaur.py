from .therapsid import Therapsid


class Archosaur(Therapsid):
    CRITTER_TAGS = frozenset({"animal", "terrestrial", "vertebrate", "predator"})
    """A therapsid-like land hunter on the archosaur evolutionary branch."""

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = (135, 150, 95)
        self.sprite = "archosaur"
