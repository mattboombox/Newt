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
windowWidth, windowHeight = board.ensureDisplaySize(800, 800)
print(windowWidth, windowHeight)
windowTitle = "Pygame Window"
windowColor = (0, 0, 0)

#Game board
cols = windowWidth // 10
rows = windowHeight // 10
Board = board.createBoard(rows, cols)
board.printBoard(Board)

#Movers
player = mover.mover(Board, 0, 0,'Q','X')
handIndex = 0
hand = tiles.tiles[0]["char"]

critter = mover.mover(Board, random.randint(0, rows - 1), random.randint(0, cols - 1), 'P', 'X')

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
            row, col = board.getClickedTile(mousePos[0] , mousePos[1])
            #print(mousePos[0], mousePos[1])
            #print(p,q)
            Board[row][col] = hand
            #board.printBoard(Board)

        if event.type == pygame.KEYDOWN:
            if event.key == pygame.K_w:
                 player.move(Board, rows, cols, 0)
            elif event.key == pygame.K_s:
                 player.move(Board, rows, cols, 2)
            elif event.key == pygame.K_a:
                 player.move(Board, rows, cols, 3)
            elif event.key == pygame.K_d:
                 player.move(Board, rows, cols, 1)
            elif event.key == pygame.K_r:
                handIndex = (handIndex + 1) % len(tiles.tiles)
                hand = tiles.tiles[handIndex]["char"]
                print("Hand:", tiles.tiles[handIndex]["name"])
                 
    #Movers
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