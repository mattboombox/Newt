from entity_cleanup import clear_tile_occupants
from impact import trigger_impact_event
from tectonics import remove_volcano_at, sync_volcano_at_tile, trigger_trench_event, trigger_uplift_event
from lake import convert_landlocked_ocean_to_lake
from tsunami import Tsunami


def spawn_tsunami(game, center_tile, max_steps=8, interval=0.2):
    if center_tile is None:
        return False

    if center_tile.terrain not in ("ocean", "trench", "shallows", "lake"):
        return False

    clear_tile_occupants(game, center_tile, "it was hit by a tsunami")
    game.tsunamis.append(Tsunami(center_tile.x, center_tile.y, max_steps=max_steps, interval=interval))
    print(f"Spawned tsunami at ({center_tile.x}, {center_tile.y})")
    return True


def trigger_event_tool(game, center_tile, event_name):
    if center_tile is None:
        return False

    if event_name == "meteor":
        remove_volcano_at(game, center_tile.x, center_tile.y)
        return trigger_impact_event(game, center_tile.x, center_tile.y, impact_type="meteor")

    if event_name == "mega_meteor":
        remove_volcano_at(game, center_tile.x, center_tile.y)
        return trigger_impact_event(
            game,
            center_tile.x,
            center_tile.y,
            impact_type="mega_meteor",
            radius_multiplier=4,
        )

    if event_name == "comet":
        remove_volcano_at(game, center_tile.x, center_tile.y)
        return trigger_impact_event(game, center_tile.x, center_tile.y, impact_type="comet")

    if event_name == "tectonic_uplift":
        remove_volcano_at(game, center_tile.x, center_tile.y)
        trigger_uplift_event(game, center_tile.x, center_tile.y)
        convert_landlocked_ocean_to_lake(game.world)
        return True

    if event_name == "trench_event":
        if not trigger_trench_event(game, center_tile.x, center_tile.y):
            print("Trench event requires open ocean.")
            return False
        return True

    if event_name == "tsunami":
        return spawn_tsunami(game, center_tile)

    return False


def paint_radius(game, center_tile, terrain_name, radius=0):
    if center_tile is None:
        return

    for dx in range(-radius, radius + 1):
        for dy in range(-radius, radius + 1):
            x = center_tile.x + dx
            y = center_tile.y + dy

            tile = game.world.get_tile(x, y)
            if tile is not None:
                sync_volcano_at_tile(game, tile, terrain_name)
                tile.set_terrain(terrain_name)

    convert_landlocked_ocean_to_lake(game.world)
