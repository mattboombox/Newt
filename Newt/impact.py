import random

def trigger_impact_event(world, x=None, y=None, min_radius=2, max_radius=4, impact_type=None):
    if world.cols <= 0 or world.rows <= 0:
        return False

    if x is None:
        x = random.randint(0, world.cols - 1)
    if y is None:
        y = random.randint(0, world.rows - 1)

    radius = random.randint(min_radius, max_radius)
    magnitude = random.randint(radius * 10, radius * 25)

    center_tile = world.get_tile(x, y)
    if center_tile is None:
        return False

    burn_radius = radius + 3

    if impact_type is None:
        impact_type = random.choice(["meteor", "comet"])

    protected_terrain = {
        "ocean",
        "shallows",
        "lake",
        "mountain",
        "active_volcano",
        "dormant_volcano",
        "lava",
    }

    for dx in range(-burn_radius, burn_radius + 1):
        for dy in range(-burn_radius, burn_radius + 1):
            tile = world.get_tile(x + dx, y + dy)
            if tile is None:
                continue

            distance_sq = dx * dx + dy * dy

            # Core
            if distance_sq <= radius * radius:
                if impact_type == "comet":
                    tile.set_terrain("lake")
                else:
                    tile.set_terrain("lava")
                continue

            # Everything outside core becomes scorched stone
            if distance_sq <= burn_radius * burn_radius:
                if tile.terrain not in protected_terrain:
                    tile.set_terrain("stone")

    if impact_type == "comet":
        center_tile.set_terrain("lake")
    else:
        center_tile.set_terrain("lava")

    print(f"{impact_type.capitalize()} impact! Magnitude {magnitude} at ({x}, {y}) with radius {radius}")
    return True