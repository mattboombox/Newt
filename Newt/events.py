from erosion import trigger_random_erosion, apply_polar_climate
from entity_cleanup import clear_tile_occupants
from life import trigger_random_growth, trigger_random_plankton_growth
from impact import trigger_impact_event
from lake import convert_landlocked_ocean_to_lake
from tectonics import spawn_dormant_volcano, trigger_island_uplift_event, trigger_trench_event, trigger_uplift_event
from tsunami import Tsunami
import random


def update_events(game, dt):
    game.erosion_timer += dt
    while game.erosion_timer >= game.erosion_interval:
        game.erosion_timer -= game.erosion_interval
        trigger_random_erosion(game.world)

    game.life_timer += dt
    if game.life_timer >= game.life_interval:
        game.life_timer = 0.0
        trigger_random_growth(game.world)
        spawned_plankton = trigger_random_plankton_growth(game.world)
        if spawned_plankton is not None:
            game.critters.append(spawned_plankton)

    game.impact_timer += dt
    if game.impact_timer >= game.impact_interval:
        game.impact_timer = 0.0
        if random.random() < game.impact_chance:
            trigger_impact_event(game)

    for volcano in game.volcanoes[:]:
        volcano.update(game, dt)

    for tsunami in game.tsunamis[:]:
        tsunami.update(game, dt)

    for impact_wave in game.impact_waves[:]:
        impact_wave.update(game, dt)

    game.tectonic_timer += dt
    if game.tectonic_timer >= game.tectonic_interval:
        game.tectonic_timer = 0.0

        if random.random() < 0.001:
            start_x = random.randint(0, game.world.cols - 1)
            start_y = random.randint(0, game.world.rows - 1)
            trigger_uplift_event(game, start_x, start_y)
            convert_landlocked_ocean_to_lake(game.world)

        if random.random() < 0.0005:
            start_x = random.randint(0, game.world.cols - 1)
            start_y = random.randint(0, game.world.rows - 1)
            trigger_island_uplift_event(game, start_x, start_y)
            convert_landlocked_ocean_to_lake(game.world)

        if random.random() < 0.0008:
            x = random.randint(0, game.world.cols - 1)
            y = random.randint(0, game.world.rows - 1)

            tile = game.world.get_tile(x, y)
            if tile is not None and tile.terrain == "ocean":
                trigger_trench_event(game, x, y)

        if random.random() < 0.001:
            x = random.randint(0, game.world.cols - 1)
            y = random.randint(0, game.world.rows - 1)
            print(f"Spawning dormant volcano at ({x}, {y})")
            spawn_dormant_volcano(game, x, y)

        if random.random() < 0.0005:
            x = random.randint(0, game.world.cols - 1)
            y = random.randint(0, game.world.rows - 1)

            tile = game.world.get_tile(x, y)
            if tile is not None and tile.terrain in ("ocean", "trench", "shallows"):
                print(f"Spawning tsunami at ({x}, {y})")
                clear_tile_occupants(game, tile, "it was hit by a tsunami")
                game.tsunamis.append(Tsunami(x, y, max_steps=12, interval=0.2))

    game.polar_timer += dt
    if game.polar_timer >= game.polar_interval:
        game.polar_timer = 0.0
        apply_polar_climate(game.world)
