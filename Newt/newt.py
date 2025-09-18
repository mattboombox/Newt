import pygame, sys, random, critter, volcano, erosion, brush
from tile import Tile
from printControls import printControls
from terrain import terrainLib

#Initialize Pygame
pygame.init()

#Tik speed variables
slowest = 200
slow = 100
fast = 10
fastest = 1
gameSpeed = slow
paused = False

#Odds
common = 10
uncommon = 100
rare = 500
rarer = 1000
unique = 10000
astronomical = 100000

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

#Set brush
paintBrush = brush.Brush()
paintBrush.setBrush("stone")

#Initial terrain gen
numIslands = 5
for _ in range(numIslands):
    volcano.getIslandSeed(board)

#Critters
initialCritters = 0
maxCritters = 1000
critterList = []

#Spawn initial critters
from critterSpawner import spawnCritter
for n in range(initialCritters):
    if len(critterList) < maxCritters:
        newCritter = spawnCritter(board)
        newCritter.name = n
        critterList.append(newCritter)
    else:
        print("Critter limit reached!", len(critterList))
        break

print("Number of critters:", len(critterList))

#Create the display window
screen = pygame.display.set_mode((windowWidth, windowHeight))
pygame.display.set_caption(windowTitle)
printControls()

tic = 0

#Main loop
running = True
while running:

    #Handle events
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running = False

        elif event.type == pygame.MOUSEBUTTONDOWN:
            if event.button == 2:  # middle: describe
                mouseX, mouseY = event.pos
                clickedTile[0] = mouseX // 10
                clickedTile[1] = mouseY // 10
                board[clickedTile[0]][clickedTile[1]].describe()

            elif event.button == 1:  # left: paint
                mouseX, mouseY = event.pos
                clickedTile[0] = mouseX // 10
                clickedTile[1] = mouseY // 10
                paintBrush.paint(board, clickedTile[0], clickedTile[1])

            elif event.button == 3:  # right: pick terrain into brush (eyedropper)
                mouseX, mouseY = event.pos
                tx, ty = mouseX // 10, mouseY // 10
                picked = board[tx][ty].terrain.name
                paintBrush.setBrush(picked)
                print(f"Picked brush: {picked}")

        elif event.type == pygame.MOUSEMOTION:
            if pygame.mouse.get_pressed(num_buttons=3)[0]:  # right button held
                mx, my = event.pos
                tx, ty = mx // 10, my // 10
                paintBrush.paint(board, tx, ty)

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
                printControls()

            if event.key == pygame.K_m:
                if len(critterList) < maxCritters:
                    newCritter = spawnCritter(board)
                    newCritter.name = (len(critterList) - 1)
                    critterList.append(newCritter)
                    print("Number of critters:", len(critterList))
                else:
                    print("Critter limit reached!", len(critterList))
                break

            if event.key == pygame.K_o: #cycle forward
                paintBrush.cycleBrush(forward=True)

            if event.key == pygame.K_i: #cycle backward
                paintBrush.cycleBrush(forward=False)

                
    #Terrain testing
    if (random.randint(0, common) == 1):
        volcano.eruptVolcano(board)
       
    if (random.randint(0, common) == 1):   
        volcano.coolLava(board)

    if (random.randint(0, rare) == 1):   
        volcano.toggleVolcano(board)

    if (random.randint(0, unique) == 1):   
        volcano.killVolcano(board)

    if (random.randint(0, rarer) == 1):   
        erosion.erodeStone(board)

    if (random.randint(0, astronomical) == 1):   
        volcano.getIslandSeed(board)

    if (random.randint(0, rare) == 1):   
        erosion.turnToSoil(board)

    if (random.randint(0, rare) == 1):   
        erosion.spawnLake(board)

    if (random.randint(0, rare) == 1):   
        erosion.spawnGrass(board)

    #Fill the screen with the background color
    screen.fill(windowColor)

    #Change the color of a critter randomly
    #if(random.randint(0, 100) == 0):
        #print("Mutation!")
        #critterList[random.randint(0, len(critterList) - 1)].color = (random.randint(0, 255),random.randint(0, 255),random.randint(0, 255))

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