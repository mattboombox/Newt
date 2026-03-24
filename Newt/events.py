from erosion import trigger_random_erosion


def update_events(game, dt):
    game.erosion_timer += dt

    if game.erosion_timer >= game.erosion_interval:
        game.erosion_timer = 0.0
        trigger_random_erosion(game.world)