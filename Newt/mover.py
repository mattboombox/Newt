import random

class mover:
    def __init__(self, board, posX, posY, icon, under):
        self.posX = posX # col
        self.posY = posY # row
        self.icon = icon
        self.under = board[self.posY][self.posX]
        board[self.posY][self.posX] = self.icon

    def isOccupied(self, board, row, col):
        #Checks if position is occupied by impassible char
        return board[row][col].isupper()

    def canMove(self, board, order, cols, rows):
        #Checks if desired position is out of bounds or is occupied by impassible char
        rows = len(board)
        cols = len(board[0])

        directions = [(-1, 0), (0, 1), (1, 0), (0, -1)]  #(dy, dx) → N, E, S, W
        dy, dx = directions[order]
        newY, newX = self.posY + dy, self.posX + dx
        
        return (0 <= newY < rows and 0 <= newX < cols) and not self.isOccupied(board, newY, newX)

    def scoot(self, board, cols, rows, order):
        #Moves the mover if possible in the direction of order and handles replacing the tile of the previsous position
        directions = [(-1, 0), (0, 1), (1, 0), (0, -1)]  #(dy, dx) → N, E, S, W
        dy, dx = directions[order]
        newY, newX = self.posY + dy, self.posX + dx

        if self.canMove(board, order, cols, rows):
            board[self.posY][self.posX] = self.under  #Restore previous tile
            self.under = board[newY][newX]  #Save new under tile
            self.posX, self.posY = newX, newY  #Update position
            board[self.posY][self.posX] = self.icon  #Place mover in new location
        else:
            print(f"Bump {['North', 'East', 'South', 'West'][order]}!")

    def wander(self, board, cols, rows):
        #Randomly moves a mover
        self.scoot(board, cols, rows, random.randint(0, 3))

    def move(self, board, cols, rows, order):
        #Moves based off of the input of order
        self.scoot(board, cols, rows, order)



