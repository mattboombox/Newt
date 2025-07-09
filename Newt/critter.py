import random

class Critter:
    def __init__(self, x: int, y: int, color=(255,0,255)):
        self.x = x
        self.y = y
        self.color = color
        self.hp = 5

def move(critter, newX, newY, board, cols, rows):
    if not (0 <= newX < cols and 0 <= newY < rows):
        #print("Destination out of bounds")
        return
    
    if board[newX][newY].critter is not None:
        #print("Tile already occupied by another critter")
        critter.color = board[newX][newY].critter.color
        return
    
    board[critter.x][critter.y].critter = None
    critter.x, critter.y = newX, newY
    board[newX][newY].critter = critter

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

