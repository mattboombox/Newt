from erosion import trigger_random_erosion
from life import trigger_random_growth
from meteor import trigger_meteor_strike
import random

def update_events(game, dt):
    game.erosion_timer += dt

    if game.erosion_timer >= game.erosion_interval:
        game.erosion_timer = 0.0
        trigger_random_erosion(game.world)

    game.life_timer += dt
    if game.life_timer >= game.life_interval:
        game.life_timer = 0.0
        trigger_random_growth(game.world)

    game.meteor_timer += dt
    if game.meteor_timer >= game.meteor_interval:
        game.meteor_timer = 0.0
        if random.random() < game.meteor_chance:
            trigger_meteor_strike(game.world)

    for volcano in game.volcanoes[:]:
        volcano.update(game, dt)