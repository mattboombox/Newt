import pygame, sys
import math
import random
from tile import Tile
import critter

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

#Critter spawn testing
spawnX, spawnY, = 0, 0
numCritters = 100
critterList = [None for _ in range(numCritters)]

for n in range (numCritters):
    while board[spawnX][spawnY].critter != None:
        spawnX = random.randint(0, cols - 1)
        spawnY = random.randint(0, rows - 1)

    critterList[n] = critter.Critter(spawnX, spawnY)
    critterList[n].color = (random.randint(0, 255),random.randint(0, 255),random.randint(0, 255))
    board[spawnX][spawnY].critter = critterList[n]


#Create the display window
screen = pygame.display.set_mode((windowWidth, windowHeight))
pygame.display.set_caption(windowTitle)

tic = 0

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
        for c in critterList:
            critter.wander(c, board, cols, rows)      

#Quit Pygame
pygame.quit()
sys.exit()