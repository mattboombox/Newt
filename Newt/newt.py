print("working")
import pygame
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
rows = windowWidth // 10
cols = windowHeight // 10
print("Number of rows and cols", rows, cols)
Board = board.createBoard(rows, cols)
board.printBoard(Board)

#Movers
hand = 'O'
player = mover.mover(Board, 3,3,'P','X')

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
            p, q = board.getClickedTile(mousePos[0] , mousePos[1])
            #print(mousePos[0], mousePos[1])
            #print(p,q)
            Board[q][p] = hand
            #board.printBoard(Board)

        if event.type == pygame.KEYDOWN:
            if event.key == pygame.K_w:
                 player.move(Board, cols, rows, 0)
            elif event.key == pygame.K_s:
                 player.move(Board, cols, rows, 2)
            elif event.key == pygame.K_a:
                 player.move(Board, cols, rows, 3)
            elif event.key == pygame.K_d:
                 player.move(Board, cols, rows, 1)

    #Fill the screen with the background color
    screen.fill(windowColor)

    #Draw baord
    for i in range (rows):
        for j in range(cols):
            pygame.draw.rect(screen, tiles.getColor(Board[i][j]), (j * 10, i * 10, 10, 10))
    
    #Update the display
    pygame.display.flip()

#Quit Pygame
pygame.quit()
sys.exit()