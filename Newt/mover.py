import random

class mover:
    def __init__(self, posX, posY, icon, under):
        self.posX = posX
        self.posY = posY
        self.icon = icon
        self.under = under

    def wander(self, board, cols, rows):
        direction = random.randint(0,3)
        if(direction == 0):
            if(self.posY > 0):
                board[self.posX][self.posY] = self.under
                self.posY -= 1
                board[self.posX][self.posY] = self.icon
            else:
                print("Bump West!")

        if(direction == 1):
            if(self.posX < cols - 1):
                board[self.posX][self.posY] = self.under
                self.posX += 1
                board[self.posX][self.posY] = self.icon
            else:
                print("Bump South!")

        if(direction == 2):
            if(self.posY != rows - 1):
                board[self.posX][self.posY] = self.under
                self.posY += 1
                board[self.posX][self.posY] = self.icon
            else:
                print("Bump East!")

        if(direction == 3):
            if(self.posX != 0):
                board[self.posX][self.posY] = self.under
                self.posX -= 1
                board[self.posX][self.posY] = self.icon
            else:
                print("Bump North!")

    def move(self, board, cols, rows, order):
        if(order == 0):
            if(self.posY > 0):
                board[self.posX][self.posY] = self.under
                self.posY -= 1
                board[self.posX][self.posY] = self.icon
            else:
                print("Bump West!")

        if(order == 1):
            if(self.posX < cols - 1):
                board[self.posX][self.posY] = self.under
                self.posX += 1
                board[self.posX][self.posY] = self.icon
            else:
                print("Bump South!")

        if(order == 2):
            if(self.posY != rows - 1):
                board[self.posX][self.posY] = self.under
                self.posY += 1
                board[self.posX][self.posY] = self.icon
            else:
                print("Bump East!")

        if(order == 3):
            if(self.posX != 0):
                board[self.posX][self.posY] = self.under
                self.posX -= 1
                board[self.posX][self.posY] = self.icon
            else:
                print("Bump North!")


