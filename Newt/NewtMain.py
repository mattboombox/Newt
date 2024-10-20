import pygame
import tile

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

sand = tile.tile(10,(100,200,30),10,10)

running = True
while(running):
    for event in pygame.event.get():
        if event.type == pygame.QUIT:
            running  = False

    sand.posx = sand.posx + 0.1

    if sand.posx > 1:
        sand.posy =sand.posy + .01


    screen.fill(color)
    pygame.draw.rect(screen,(sand.color),(sand.size,sand.size,sand.posx,sand.posy))
    pygame.display.flip()
    