import random
from critter import Critter

def spawnCritter(board):
    cols = len(board)
    rows = len(board[0])

    # Collect all tiles without a critter
    critterless = [(x, y) for x in range(cols) for y in range(rows)
                   if board[x][y].critter is None]

    if not critterless:
        print("Could not find any critterless tiles!")
        return None  # No space to spawn

    # Pick a random critterless tile
    spawnX, spawnY = random.choice(critterless)

    # Birth the critter
    newCritter = Critter(spawnX, spawnY, 0)
    newCritter.color = (
        random.randint(0, 255),
        random.randint(0, 255),
        random.randint(0, 255)
    )

    # Place the critter
    board[spawnX][spawnY].critter = newCritter

    return newCritter