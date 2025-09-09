import random
from critter import Critter

def spawnCritter(board):
    cols = len(board)
    rows = len(board[0])

    #Find random critterless tile
    while True:
        spawnX = random.randint(0, cols - 1)
        spawnY = random.randint(0, rows - 1)
        if board[spawnX][spawnY].critter is None:
            break

    #Birth the critter
    newCritter = Critter(spawnX, spawnY, 0)
    newCritter.color = (random.randint(0, 255), random.randint(0, 255), random.randint(0, 255))

    #Place the critter
    board[spawnX][spawnY].critter = newCritter

    return newCritter
