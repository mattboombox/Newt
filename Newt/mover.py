import random

class mover:
    def __init__(self, board, posX, posY, icon, under):
        self.posX = posX
        self.posY = posY
        self.icon = icon
        self.under = board[self.posX][self.posY]

    def scoot(self, board, cols, rows, order):
        if(order == 0):
            if(self.posY > 0):
                board[self.posX][self.posY] = self.under
                self.under = board[self.posX][self.posY - 1]
                self.posY -= 1
                board[self.posX][self.posY] = self.icon
            else:
                print("Bump West!")

        if(order == 1):
            if(self.posX < cols - 1):
                board[self.posX][self.posY] = self.under
                self.under = board[self.posX + 1][self.posY]
                self.posX += 1
                board[self.posX][self.posY] = self.icon
            else:
                print("Bump South!")

        if(order == 2):
            if(self.posY != rows - 1):
                board[self.posX][self.posY] = self.under
                self.under = board[self.posX][self.posY + 1]
                self.posY += 1
                board[self.posX][self.posY] = self.icon
            else:
                print("Bump East!")

        if(order == 3):
            if(self.posX != 0):
                board[self.posX][self.posY] = self.under
                self.under = board[self.posX - 1][self.posY]
                self.posX -= 1
                board[self.posX][self.posY] = self.icon
            else:
                print("Bump North!")

    def wander(self, board, cols, rows):
        order = random.randint(0,3)
        self.scoot(board, cols, rows, order)

    def move(self, board, cols, rows, order):
        self.scoot(board, cols, rows, order)



