import pygame, sys
import random
import math
from tile import Tile
from critter import Critter

#Initialize Pygame
pygame.init()

#Set up the display
windowWidth = 900
windowHeight = 400
print(windowWidth, windowHeight)
windowTitle = "Pygame Window"
windowColor = (25, 25, 25)

#Board
cols = windowWidth // 10
rows = windowHeight // 10
boardCenter = windowWidth // 2, windowHeight // 2
print (boardCenter)
board = [[None for _ in range(rows)] for _ in range(cols)]

#Fill board with default tile
for x in range (cols):
    for y in range (rows):
        board[x][y] = Tile(x, y)

#Critter testing
spawnX, spawnY, = 0, 0
newCritter = Critter(spawnX, spawnY)
board[spawnX][spawnY].critter = newCritter

#Critter functions
def moveCritter(critter, newX, newY, board, cols, rows):
    if not (0 <= newX < cols and 0 <= newY < rows):
        print("Destination out of bounds")
        return
    
    if board[newX][newY].critter is not None:
        print("Tile already occupied by another critter")
        return
    
    board[critter.x][critter.y].critter = None
    critter.x, critter.y = newX, newY
    board[newX][newY].critter = critter

#Create the display window
screen = pygame.display.set_mode((windowWidth, windowHeight))
pygame.display.set_caption(windowTitle)

tic = 0
newX = 1

#Main loop
running = True
while running:
    #Handle events
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running = False

            
    #Fill the screen with the background color
    screen.fill(windowColor)

    #Draw board
    for x in range (cols):
        for y in range(rows):
            pygame.draw.rect(screen, board[x][y].getColor(), (x * 10, y * 10, 10, 10))

    #Update the display
    pygame.display.flip()

    tic = tic + 1
    if tic % 100 == 0:
        tic = 0
        newX = newX + 1
        moveCritter(newCritter, newX, 0, board, cols, rows)        


#Quit Pygame
pygame.quit()
sys.exit()