import random

class mover:
    def __init__(self, board, posX, posY, icon, under):
        self.posX = posX
        self.posY = posY
        self.icon = icon
        self.under = board[self.posX][self.posY]
        board[self.posX][self.posY] = self.icon

    def isOccupied(self, board, x, y):
        return board[x][y].isupper()

    def canMove(self, board, order, cols, rows):
        directions = [(-1, 0), (0, 1), (1, 0), (0, -1)]  # N, E, S, W
        dx, dy = directions[order]
        newX, newY = self.posX + dx, self.posY + dy
        
        return (0 <= newX < cols and 0 <= newY < rows) and not self.isOccupied(board, newX, newY)

    def scoot(self, board, cols, rows, order):
        directions = [(-1, 0), (0, 1), (1, 0), (0, -1)]  # N, E, S, W
        dx, dy = directions[order]
        newX, newY = self.posX + dx, self.posY + dy

        if self.canMove(board, order, cols, rows):
            board[self.posX][self.posY] = self.under  # Restore previous tile
            self.under = board[newX][newY]  # Save new under tile
            self.posX, self.posY = newX, newY  # Update position
            board[self.posX][self.posY] = self.icon  # Place mover
        else:
            print(f"Bump {['North', 'East', 'South', 'West'][order]}!")

    def wander(self, board, cols, rows):
        self.scoot(board, cols, rows, random.randint(0, 3))

    def move(self, board, cols, rows, order):
        self.scoot(board, cols, rows, order)



