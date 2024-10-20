import pygame
import player.py

pygame.init()

print("Hello")

screen_width = 500
screen_height = 500
screen = pygame.display.set_mode((screen_width,screen_height))
pygame.display.set_caption("Newt")

r = 20
g = 50
b = 200
color = (r,g,b)

running = True
while(running):
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running  = False

    screen.fill(color)
    pygame.display.flip()
    