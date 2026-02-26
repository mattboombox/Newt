import random
from critter import Critter


def _random_empty_tile_of_terrain(board, terrain_name: str, tries: int = 500):
    """
    Fast path: randomly sample tiles looking for an empty tile with terrain_name.
    Fallback: full scan if sampling fails (map might be dense).
    """
    cols = len(board)
    rows = len(board[0])

    # Random sampling
    for _ in range(tries):
        x = random.randrange(cols)
        y = random.randrange(rows)
        t = board[x][y]
        if t.critter is None and t.terrain.name == terrain_name:
            return x, y

    # Fallback scan (rare)
    spots = [
        (x, y)
        for x in range(cols)
        for y in range(rows)
        if board[x][y].critter is None and board[x][y].terrain.name == terrain_name
    ]
    if not spots:
        return None
    return random.choice(spots)


def spawnFishCritter(board):
    pos = _random_empty_tile_of_terrain(board, "shallows")
    if pos is None:
        print("No empty shallows tiles available for fish spawn.")
        return None

    x, y = pos
    c = Critter(x, y, name="fish", species="fish", fish=True)
    c.color = (
        random.randint(0, 80),
        random.randint(0, 140),
        random.randint(180, 255),
    )

    board[x][y].critter = c
    return c


def spawnLandCritter(board):
    pos = _random_empty_tile_of_terrain(board, "grass")
    if pos is None:
        print("No empty grass tiles available for land spawn.")
        return None

    x, y = pos
    c = Critter(x, y, name="landCritter", species="deer", fish=False)
    c.color = (
        random.randint(80, 180),
        random.randint(90, 170),
        random.randint(40, 120),
    )

    board[x][y].critter = c
    return c