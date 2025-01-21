print("working")
import pygame
import random
import board
import tiles
import sys

#Initialize Pygame
pygame.init()

#Set up the display
window_width = 800
window_height = 800
window_title = "Pygame Window"
window_color = (0, 0, 0)

#Game board
rows = 80
cols = 80
Board = board.create_board(rows, cols)
board.print_board(Board)

#Generate terrain

#Mover prototype
moverX = random.randint(0, rows)
moverY = random.randint(0, cols)
alive = True
mover_char = 'p'




#Create the display window
screen = pygame.display.set_mode((window_width, window_height))
pygame.display.set_caption(window_title)

# Main game loop
running = True
while running:
    #Handle events
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running = False

        if event.type == pygame.MOUSEBUTTONDOWN:
            mouse_pos = pygame.mouse.get_pos()
            p, q = board.get_clicked_tile(mouse_pos[0] , mouse_pos[1])
            print(mouse_pos[0], mouse_pos[1])
            print(p,q)
            Board[q][p] = 'X'
            board.print_board(Board)

        if (alive):
            direction = random.randint(0,3)
            print(moverX, moverY)
            if direction == 0: #North
                Board[moverX][moverY] = 'G'
                moverY = moverY + 1
                Board[moverX][moverY] = mover_char
            
            if direction == 1: #East
                Board[moverX][moverY] = 'G'
                moverX = moverX + 1
                Board[moverX][moverY] = mover_char

            if direction == 2: #South
                Board[moverX][moverY] = 'G'
                moverY = moverY - 1
                Board[moverX][moverY] = mover_char

            if direction == 3: #West
                Board[moverX][moverY] = 'G'
                moverX = moverX - 1
                Board[moverX][moverY] = mover_char

           
    #update mover
    Board[moverX][moverY] = mover_char

    #Fill the screen with the background color
    screen.fill(window_color)

    #Draw baord
    for i in range (rows):
        for j in range(cols):
            pygame.draw.rect(screen, tiles.get_color(Board[i][j]), (j * 10, i * 10, 10, 10))
    

    #Update the display
    pygame.display.flip()

#Quit Pygame
pygame.quit()
sys.exit()