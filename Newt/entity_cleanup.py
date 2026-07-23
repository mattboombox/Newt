from city import City


def describe_building(building):
    if isinstance(building, City):
        return f"{building.level.title()} city"

    return type(building).__name__


def remove_critter(game, critter, reason):
    if hasattr(critter, "clear_home_building"):
        critter.clear_home_building()

    tile = game.world.get_tile(critter.x, critter.y)
    if tile is not None and tile.critter is critter:
        tile.critter = None

    if critter in game.critters:
        game.critters.remove(critter)
    if hasattr(game, "dying_critters"):
        game.dying_critters.discard(critter)
    return True


def remove_building_at_tile(game, tile, reason):
    building = tile.building
    if building is None:
        return False

    building.on_removed(game)
    tile.building = None
    print(
        f"Removed {describe_building(building)} at "
        f"({tile.x}, {tile.y}) because {reason}."
    )
    return True


def clear_tile_occupants(game, tile, reason, preserve_water_habitable_critters=False):
    if tile is None:
        return

    if (
        tile.critter is not None
        and not (
            preserve_water_habitable_critters
            and tile.has_tag("water")
            and tile.critter.is_habitable_tile(tile)
        )
    ):
        remove_critter(game, tile.critter, reason)

    if tile.building is not None:
        remove_building_at_tile(game, tile, reason)


def clear_stale_tile_critters(game):
    active_critters = set(game.critters)

    for x in range(game.world.cols):
        for y in range(game.world.rows):
            tile = game.world.board[x][y]
            critter = tile.critter
            if critter is None:
                continue

            if critter not in active_critters or critter.x != x or critter.y != y:
                tile.critter = None


def remove_stranded_critters(game):
    for critter in game.critters[:]:
        tile = game.world.get_tile(critter.x, critter.y)
        if critter.is_habitable_tile(tile):
            continue

        if getattr(critter, "needs_habitat_relocation", False):
            continue

        if tile is not None and tile.terrain == "shallows":
            continue

        terrain_name = "missing terrain" if tile is None else tile.terrain
        remove_critter(
            game,
            critter,
            f"it was stranded on {terrain_name} and could no longer move",
        )
