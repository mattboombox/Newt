import random
from terrain import LIQUID, IMPASSIBLE


class Critter:
    __slots__ = ("x", "y", "color", "fish", "name", "species")

    def __init__(self, x: int, y: int, name: str, fish: bool, species: str, color=(255, 0, 255)):
        self.x = x
        self.y = y
        self.color = color
        self.fish = fish
        self.name = name
        self.species = species


# 9 directions (0 = stay)
_DIRS = (
    (0, 0),   # stay
    (0, 1),   # N
    (1, 1),   # NE
    (1, 0),   # E
    (1, -1),  # SE
    (0, -1),  # S
    (-1, -1), # SW
    (-1, 0),  # W
    (-1, 1),  # NW
)


def move(c, new_x: int, new_y: int, board, cols: int, rows: int) -> bool:
    """Attempt move. Returns True if moved."""
    # bounds
    if new_x < 0 or new_x >= cols or new_y < 0 or new_y >= rows:
        return False

    dest_tile = board[new_x][new_y]
    dest_type = dest_tile.terrain.type  # int enum

    # impassible blocks all
    if dest_type == IMPASSIBLE:
        return False

    # fish must stay in liquid; non-fish must stay out of liquid
    if c.fish:
        if dest_type != LIQUID:
            return False
    else:
        if dest_type == LIQUID:
            return False

    # occupied
    if dest_tile.critter is not None:
        return False

    # perform move
    board[c.x][c.y].critter = None
    c.x, c.y = new_x, new_y
    dest_tile.critter = c
    return True


def wander(c, board, cols: int, rows: int) -> bool:
    """Random step (including stay). Returns True if moved."""
    dx, dy = _DIRS[random.randrange(9)]
    if dx == 0 and dy == 0:
        return False
    return move(c, c.x + dx, c.y + dy, board, cols, rows)

