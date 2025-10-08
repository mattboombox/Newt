import random
from critter import Critter

def spawnFishCritter(board):
    cols = len(board)
    rows = len(board[0])

    # Empty shallows tiles only
    shallows_spots = [
        (x, y)
        for x in range(cols)
        for y in range(rows)
        if board[x][y].critter is None and board[x][y].terrain.name == "shallows"
    ]

    if not shallows_spots:
        print("No empty shallows tiles available for fish spawn.")
        return None

    spawnX, spawnY = random.choice(shallows_spots)

    # Create a fish critter (name as string; fish=True)
    newCritter = Critter(spawnX, spawnY, name="fish", species = "fish", fish=True)
    newCritter.color = (
        random.randint(0, 80),    # R: low
        random.randint(0, 140),   # G: mid-low
        random.randint(180, 255)  # B: high
    )

    board[spawnX][spawnY].critter = newCritter
    return newCritter

def spawnLandCritter(board):
    cols = len(board)
    rows = len(board[0])

    # Empty grass tiles only
    grass_spots = [
        (x, y)
        for x in range(cols)
        for y in range(rows)
        if board[x][y].critter is None and board[x][y].terrain.name == "grass"
    ]

    if not grass_spots:
        print("No empty grass tiles available for land spawn.")
        return None

    spawnX, spawnY = random.choice(grass_spots)

    # Create a land critter (fish=False)
    newCritter = Critter(spawnX, spawnY, name="landCritter", species = "deer", fish=False)
    newCritter.color = (
        random.randint(80, 180),   # earthy-ish tones
        random.randint(90, 170),
        random.randint(40, 120)
    )

    board[spawnX][spawnY].critter = newCritter
    return newCritter