import random
from terrain import LIQUID, IMPASSIBLE

class Critter:
    def __init__(self, x: int, y: int, name: str, fish: bool, color=(255,0,255)):
        self.x = x
        self.y = y
        self.color = color
        self.fish = fish
        self.name = name

def move(critter, newX, newY, board, cols, rows):
    # bounds
    if not (0 <= newX < cols and 0 <= newY < rows):
        return

    dest_tile = board[newX][newY]
    dest_type = dest_tile.terrain.type  # SOLID/LIQUID/IMPASSIBLE as ints

    # block impassible for all critters
    if dest_type == IMPASSIBLE:
        return

    # fish can ONLY move on liquid
    if critter.fish:
        if dest_type != LIQUID:
            return
    else:
        # non-fish cannot move on liquid
        if dest_type == LIQUID:
            return

    # occupied?
    if dest_tile.critter is not None:
        # visual “battle” effect
        #critter.color = dest_tile.critter.color
        return

    # perform move
    board[critter.x][critter.y].critter = None
    critter.x, critter.y = newX, newY
    dest_tile.critter = critter
    # Optionally: dest_tile.terrain.color = critter.color

def wander(critter, board, cols, rows):
    direction = random.randint(0, 8)

    if direction == 0:#stay
        #move(critter, critter.x,     critter.y,     board, cols, rows)
        return
    elif direction == 1:#N
        move(critter, critter.x,     critter.y + 1, board, cols, rows)
    elif direction == 2:#NE
        move(critter, critter.x + 1, critter.y + 1, board, cols, rows)
    elif direction == 3:#E
        move(critter, critter.x + 1, critter.y,     board, cols, rows)
    elif direction == 4:#SE
        move(critter, critter.x + 1, critter.y - 1, board, cols, rows)
    elif direction == 5:#S
        move(critter, critter.x,     critter.y - 1, board, cols, rows)
    elif direction == 6:#SW
        move(critter, critter.x - 1, critter.y - 1, board, cols, rows)
    elif direction == 7:#W
        move(critter, critter.x - 1, critter.y,     board, cols, rows)
    elif direction == 8:#NW
        move(critter, critter.x - 1, critter.y + 1, board, cols, rows)

