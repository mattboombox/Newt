import pygame, sys, random, critter, volcano, erosion, brush, critterSpawner
from tile import Tile
from printControls import printControls

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
TILE = 10
cols = windowWidth // TILE
rows = windowHeight // TILE
boardCenter = windowWidth // 2, windowHeight // 2
clickedTile = [0,0]
board = [[None for _ in range(rows)] for _ in range(cols)]
print("Number of tiles:", cols, "x", rows, "=", rows*cols)

# ---- Helper: safe mouse->tile with bounds check ----
def pos_to_tile(mx, my):
    tx = mx // TILE
    ty = my // TILE
    if 0 <= tx < cols and 0 <= ty < rows:
        return tx, ty
    return None

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
maxCritters = 1000
critterList = []

#Create the display window
screen = pygame.display.set_mode((windowWidth, windowHeight))
pygame.display.set_caption(windowTitle)
printControls()

#load sprites
sprites = {
    "deer": pygame.image.load("Newt/sprites/deer.png").convert_alpha(),
    "fish": pygame.image.load("Newt/sprites/fish.png").convert_alpha(),
}

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
                pos = pos_to_tile(*event.pos)
                if pos:
                    tx, ty = pos
                    clickedTile[0], clickedTile[1] = tx, ty
                    board[tx][ty].describe()

            elif event.button == 1:  # left: paint
                pos = pos_to_tile(*event.pos)
                if pos:
                    tx, ty = pos
                    clickedTile[0], clickedTile[1] = tx, ty
                    paintBrush.paint(board, tx, ty)

            elif event.button == 3:  # right: pick terrain into brush (eyedropper)
                pos = pos_to_tile(*event.pos)
                if pos:
                    tx, ty = pos
                    picked = board[tx][ty].terrain.name
                    paintBrush.setBrush(picked)
                    print(f"Picked brush: {picked}")

        elif event.type == pygame.MOUSEMOTION:
            # left button held (comment fixed)
            if pygame.mouse.get_pressed(num_buttons=3)[0]:
                pos = pos_to_tile(*event.pos)
                if pos:
                    tx, ty = pos
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
                    # 50/50: pick which to try first
                    if random.random() < 0.5:
                        order = (critterSpawner.spawnFishCritter, critterSpawner.spawnLandCritter)
                    else:
                        order = (critterSpawner.spawnLandCritter, critterSpawner.spawnFishCritter)

                    newCritter = None
                    for fn in order:
                        newCritter = fn(board)
                        if newCritter is not None:
                            break

                    if newCritter is None:
                        print("No valid tiles to spawn a fish or land critter.")
                        break

                    # Give it an ID/name before appending (0-based)
                    newCritter.name = str(len(critterList))
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
        volcano.eruptVolcano(board, critterList)

    if (random.randint(0, common) == 1):
        volcano.coolLava(board)

    if (random.randint(0, rare) == 1):
        volcano.toggleVolcano(board)

    if (random.randint(0, unique) == 1):
        volcano.killVolcano(board)

    if (random.randint(0, uncommon) == 1):
        erosion.spawnDesert(board)

    if (random.randint(0, uncommon) == 1):
        erosion.erodeCoast(board)

    if (random.randint(0, astronomical) == 1):
        volcano.getIslandSeed(board)

    if (random.randint(0, rare) == 1):
        erosion.spawnLake(board)

    if (random.randint(0, rare) == 1):
        erosion.spawnGrass(board)

    if (random.randint(0, rare) == 1):
        erosion.spawnReef(board)

    if (random.randint(0, astronomical) == 1):
        erosion.meteorStrike(board, critterList)

    if random.randint(0, unique) == 1:
        if len(critterList) < maxCritters:
            # 50/50: pick which to try first
            if random.random() < 0.5:
                order = (critterSpawner.spawnFishCritter, critterSpawner.spawnLandCritter)
            else:
                order = (critterSpawner.spawnLandCritter, critterSpawner.spawnFishCritter)

            newCritter = None
            for fn in order:
                newCritter = fn(board)
                if newCritter is not None:
                    break

            if newCritter is None:
                print("No valid tiles to spawn a fish or land critter.")
            else:
                # Give it an ID/name before appending (0-based)
                newCritter.name = str(len(critterList))
                critterList.append(newCritter)
                if newCritter.fish:
                    print("A fish has spawned at", newCritter.x, newCritter.y, "!")
                else:
                    print("A land dweller has spawned at", newCritter.x, newCritter.y, "!")
                print("Number of critters:", len(critterList))
        else:
            print("Critter limit reached!", len(critterList))

    #Fill the screen with the background color
    screen.fill(windowColor)

    #Draw board
    for x in range (cols):
        for y in range(rows):
            pygame.draw.rect(screen, board[x][y].getTerrainColor(), (x * TILE, y * TILE, TILE, TILE))
            if board[x][y].critter is not None:
                screen.blit(sprites[board[x][y].critter.species], (x * TILE, y * TILE))

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