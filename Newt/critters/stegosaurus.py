from .deer import Deer


class Stegosaurus(Deer):
    """A deer-like grazing archosaur."""

    def __init__(self, x, y):
        super().__init__(x, y)
        self.color = (115, 145, 85)
        self.sprite = "stegosaurus"

