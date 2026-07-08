from entity_cleanup import clear_tile_occupants
from impact import trigger_impact_event
from tectonics import remove_volcano_at, sync_volcano_at_tile, trigger_trench_event, trigger_uplift_event
from lake import convert_landlocked_ocean_to_lake
from tsunami import Tsunami


def paint_radius(game, center_tile, terrain_name, radius=0):
    if center_tile is None:
        return

    if terrain_name == "meteor":
        remove_volcano_at(game, center_tile.x, center_tile.y)
        trigger_impact_event(game, center_tile.x, center_tile.y, impact_type="meteor")
        return

    if terrain_name == "comet":
        remove_volcano_at(game, center_tile.x, center_tile.y)
        trigger_impact_event(game, center_tile.x, center_tile.y, impact_type="comet")
        return

    if terrain_name == "tectonic_uplift":
        remove_volcano_at(game, center_tile.x, center_tile.y)
        trigger_uplift_event(game, center_tile.x, center_tile.y)
        convert_landlocked_ocean_to_lake(game.world)
        return

    if terrain_name == "trench":
        if not trigger_trench_event(game, center_tile.x, center_tile.y):
            print("Trench event requires open ocean.")
        return

    if terrain_name == "tsunami":
        if center_tile.terrain in ("ocean", "trench", "shallows", "lake"):
            clear_tile_occupants(game, center_tile, "it was hit by a tsunami")
            game.tsunamis.append(Tsunami(center_tile.x, center_tile.y))
            print(f"Spawned tsunami at ({center_tile.x}, {center_tile.y})")
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
