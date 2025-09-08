import pygame, sys
import math
import random
from tile import Tile
import critter

#Initialize Pygame
pygame.init()
slowest = 200
slow = 100
fast = 10
fastest = 1
gameSpeed = slow
paused = False

#Set up the display
windowWidth = 1200
windowHeight = 800
print("Display size:", windowWidth, windowHeight)
windowTitle = "Pygame Window"
windowColor = (25, 25, 25)

#Board
cols = windowWidth // 10
rows = windowHeight // 10
boardCenter = windowWidth // 2, windowHeight // 2
clickedTile = [0,0]
board = [[None for _ in range(rows)] for _ in range(cols)]
print("Number of tiles:", cols, "x", rows, "=", rows*cols)

#Fill board with default tile
for x in range (cols):
    for y in range (rows):
        board[x][y] = Tile(x, y)

#Critters
spawnX, spawnY, = 0, 0
numCritters = 200
print("Number of critters:", numCritters)
critterList = [None for _ in range(numCritters)]

for n in range (numCritters):
    while board[spawnX][spawnY].critter != None:
        spawnX = random.randint(0, cols - 1)
        spawnY = random.randint(0, rows - 1)

    critterList[n] = critter.Critter(spawnX, spawnY, n)
    critterList[n].color = (random.randint(0, 255),random.randint(0, 255),random.randint(0, 255))
    board[spawnX][spawnY].critter = critterList[n]


#Create the display window
screen = pygame.display.set_mode((windowWidth, windowHeight))
pygame.display.set_caption(windowTitle)
print("Controls:"), print("p: pause/unpause"), print("-: slow down"), print("=: speed up"), print("x: exit"), print("h: show controls"), print("mouse click: describe tile"), print()

tic = 0

#Main loop
running = True
while running:
    #Handle events
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running = False
        elif event.type == pygame.MOUSEBUTTONDOWN:
            mouseX, mouseY = event.pos
            clickedTile[0] = mouseX // 10
            clickedTile[1] = mouseY // 10
            #print("Tile clicked:" clickedTile[0], clickedTile[1])
            board[clickedTile[0]][clickedTile[1]].describe()
        elif event.type == pygame.KEYDOWN:
            if event.key == pygame.K_EQUALS:
                if(gameSpeed == slowest):
                    print("Slow")
                    gameSpeed = slow
                elif(gameSpeed == slow):
                    print("Fast")
                    gameSpeed = fast
                elif(gameSpeed == fast):
                    gameSpeed = fastest
                    print("Fastest")
            if event.key == pygame.K_MINUS:
                    if(gameSpeed == fastest):
                        print("Fast")
                        gameSpeed = fast
                    elif(gameSpeed == fast):
                        print("Slow")
                        gameSpeed = slow
                    elif(gameSpeed == slow):
                        gameSpeed = slowest
                        print("Slowest")
            if event.key == pygame.K_p:
                    if(not paused):
                        paused = True
                        print("Paused")
                    else:
                        paused = False
                        print("Unpaused")
            if event.key == pygame.K_x:
                print("Goodbye!")
                running = False
            if event.key == pygame.K_h:
                print("Controls:"), print("p: pause/unpause"), print("-: slow down"), print("=: speed up"), print("x: exit"), print("h: show controls"), print("mouse click: describe tile"), print()
                

    #Fill the screen with the background color
    screen.fill(windowColor)

    #Change the color of a critter randomly
    #if(random.randint(0, 100) == 0):
        #print("Mutation!")
        #critterList[random.randint(0, numCritters - 1)].color = (random.randint(0, 255),random.randint(0, 255),random.randint(0, 255))

    #Draw board
    for x in range (cols):
        for y in range(rows):
            pygame.draw.rect(screen, board[x][y].getTerrainColor(), (x * 10, y * 10, 10, 10))
            if(board[x][y].critter is not None):
                pygame.draw.circle(screen, board[x][y].getThingColor(), (x * 10 + 5, y * 10 + 5), 5)
                

    #Update the display
    pygame.display.flip()

    #Move critters
    if(not paused):
        tic = tic + 1
        if tic % gameSpeed == 0:
            tic = 0
            for c in critterList:
                critter.wander(c, board, cols, rows)      

#Quit Pygame
pygame.quit()
sys.exit()