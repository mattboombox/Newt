print("working")
import pygame
import random
import sys

import tile

#Initialize Pygame
pygame.init()

#Set up the display
windowWidth = 700
windowHeight = 700
print(windowWidth, windowHeight)
windowTitle = "Pygame Window"
windowColor = (25, 25, 25)

#Game board
rows = windowHeight // 10
cols = windowWidth // 10

board = [[None for _ in range(cols)] for _ in range(rows)]

for row in range (rows):
    for col in range (cols):
        board[row][col] = tile.tile(row, col)

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
            
    #Fill the screen with the background color
    screen.fill(windowColor)

    #Draw board
    for row in range (rows):
        for col in range(cols):
            pygame.draw.rect(screen, board[row][col].color, (col * 10, row * 10, 10, 10))

    #Update the display
    pygame.display.flip()

#Quit Pygame
pygame.quit()
sys.exit()