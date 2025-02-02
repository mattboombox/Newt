print("working")
import pygame
import board
import tiles
import mover
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

#Movers
hand = 'O'
player = mover.mover(Board, 50,50,'P','X')
critter0 = mover.mover(Board, 10,10,'P','X')

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
            #print(mouse_pos[0], mouse_pos[1])
            #print(p,q)
            Board[q][p] = hand
            #board.print_board(Board)

        if event.type == pygame.KEYDOWN:
            if event.key == pygame.K_w:
                 player.move(Board, cols, rows, 0)
            elif event.key == pygame.K_s:
                 player.move(Board, cols, rows, 2)
            elif event.key == pygame.K_a:
                 player.move(Board, cols, rows, 3)
            elif event.key == pygame.K_d:
                 player.move(Board, cols, rows, 1)

            
    critter0.wander(Board, cols, rows)

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