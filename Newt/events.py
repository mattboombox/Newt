from erosion import trigger_random_erosion, apply_polar_climate
from life import trigger_random_growth
from impact import trigger_impact_event
from lake import convert_landlocked_ocean_to_lake
from tectonics import generate_uplift_chain, spawn_dormant_volcano
from tsunami import Tsunami
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

    game.impact_timer += dt
    if game.impact_timer >= game.impact_interval:
        game.impact_timer = 0.0
        if random.random() < game.impact_chance:
            trigger_impact_event(game.world)
            convert_landlocked_ocean_to_lake(game.world)

    for volcano in game.volcanoes[:]:
        volcano.update(game, dt)

    game.tectonic_timer += dt
    if game.tectonic_timer >= game.tectonic_interval:
        game.tectonic_timer = 0.0

        if random.random() < 0.001:
            start_x = random.randint(0, game.world.cols - 1)
            start_y = random.randint(0, game.world.rows - 1)
            generate_uplift_chain(game, start_x, start_y)
            convert_landlocked_ocean_to_lake(game.world)

        if random.random() < 0.001:
            x = random.randint(0, game.world.cols - 1)
            y = random.randint(0, game.world.rows - 1)
            print(f"Spawning dormant volcano at ({x}, {y})")
            spawn_dormant_volcano(game, x, y)

    # Example random tsunami chance
    if random.random() < 0.0005:
        x = random.randint(0, game.world.cols - 1)
        y = random.randint(0, game.world.rows - 1)

        tile = game.world.get_tile(x, y)
        if tile is not None and tile.terrain in ("ocean", "shallows"):
            print(f"Spawning tsunami at ({x}, {y})")
            game.tsunamis.append(Tsunami(x, y, max_steps=12, interval=0.2))

    for tsunami in game.tsunamis[:]:
        tsunami.update(game, dt)

    game.polar_timer += dt
    if game.polar_timer >= game.polar_interval:
        game.polar_timer = 0.0
        apply_polar_climate(game.world)