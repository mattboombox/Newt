import random


def trigger_meteor_strike(world, x=None, y=None, min_radius=2, max_radius=4):
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

    outer_radius = radius + 1

    for dx in range(-outer_radius, outer_radius + 1):
        for dy in range(-outer_radius, outer_radius + 1):
            tile = world.get_tile(x + dx, y + dy)
            if tile is None:
                continue

            distance_sq = dx * dx + dy * dy

            # Core impact zone: lava
            if distance_sq <= radius * radius:
                tile.set_terrain("lava")
                continue

            # Smaller outer splatter ring
            if distance_sq <= outer_radius * outer_radius:
                if random.random() < 0.18:
                    tile.set_terrain("lava")

    # Make the center tile a mountain after the impact
    center_tile.set_terrain("mountain")

    print(f"Meteor strike! Magnitude {magnitude} at ({x}, {y}) with radius {radius}")
    return True