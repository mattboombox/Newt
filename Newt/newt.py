print("working")
import pygame
import random
import board
import tiles
import mover
import sys

#Initialize Pygame
pygame.init()

#Set up the display
windowWidth, windowHeight = board.ensureDisplaySize(1200, 800)
print(windowWidth, windowHeight)
windowTitle = "Pygame Window"
windowColor = (0, 0, 0)

#Game board
cols = windowWidth // 10
rows = windowHeight // 10
Board = board.createBoard(rows, cols)
board.printBoard(Board)
phase = 1

#Terrain generation
board.generateTerrain(Board, rows, cols, 1)

#User variables
player = mover.mover(Board, 0, 0,'Q','X')
handIndex = 0
hand = tiles.tiles[0]["char"]
stampIndex = 0
stamp = tiles.stamps[0]["stamp"]
clickMode = 0

#Critters
numCritters = 25
critters = []
for _ in range(numCritters):
    x = random.randint(0, cols - 1)
    y = random.randint(0, rows - 1)
    critters.append(mover.mover(Board, x, y, 'P', 'X'))


#Create the display window
screen = pygame.display.set_mode((windowWidth, windowHeight))
pygame.display.set_caption(windowTitle)

# Main game loop
running = True
while running:
    #Handle events
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running = False

        if event.type == pygame.MOUSEBUTTONDOWN:
            mousePos = pygame.mouse.get_pos()
            row, col = board.getClickedTile(mousePos[0], mousePos[1])
            if(clickMode == 0):
                #print(mousePos[0], mousePos[1])
                #print(p,q)
                Board[row][col] = hand
                #board.printBoard(Board)
            elif(clickMode == 1):
                board.placeStamp(Board, rows, cols, stamp, row, col)
                print(board.printBoard(Board))

        if event.type == pygame.KEYDOWN:
            if event.key == pygame.K_w: #W Move player north
                 player.move(Board, rows, cols, 0)
            elif event.key == pygame.K_s: #S Move player south
                 player.move(Board, rows, cols, 2)
            elif event.key == pygame.K_a: #A Move player west
                 player.move(Board, rows, cols, 3)
            elif event.key == pygame.K_d: #D Move player east
                 player.move(Board, rows, cols, 1)
            elif event.key == pygame.K_r: #R Cycle through tiles
                clickMode = 0 #Painting
                handIndex = (handIndex + 1) % len(tiles.tiles)
                hand = tiles.tiles[handIndex]["char"]
                print("Hand:", tiles.tiles[handIndex]["name"])
            elif event.key == pygame.K_t: #T Cycle through stamps
                clickMode = 1 #Stamping
                stampIndex = (stampIndex + 1) % len(tiles.stamps)
                stamp = tiles.stamps[stampIndex]["stamp"]
                print("Stamp:", tiles.stamps[stampIndex]["name"])
            elif event.key == pygame.K_f: #F Rotate stamp
                stamp = tiles.flipStamp(stamp)
                print("Rotated stamp")


    #Critters
    for critter in critters:
        critter.wander(Board, cols, rows)

    #Fill the screen with the background color
    screen.fill(windowColor)

    #Draw baord
    for row in range (rows):
        for col in range(cols):
            
            pygame.draw.rect(screen, tiles.getColor(Board[row][col]), (col * 10, row * 10, 10, 10))
    
    #Update the display
    pygame.display.flip()

#Quit Pygame
pygame.quit()
sys.exit()