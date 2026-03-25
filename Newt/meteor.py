import random


def trigger_meteor_strike(world, x=None, y=None, min_radius=2, max_radius=5):
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

    for dx in range(-radius, radius + 1):
        for dy in range(-radius, radius + 1):
            tile = world.get_tile(x + dx, y + dy)
            if tile is None:
                continue

            distance_sq = dx * dx + dy * dy

            # Core impact zone: always lava
            if distance_sq <= radius * radius:
                tile.set_terrain("lava")
                continue

            # Outer splatter ring: noisy/random
            outer_radius = radius + 2
            if distance_sq <= outer_radius * outer_radius:
                if random.random() < 0.35:
                    tile.set_terrain("lava")

    print(f"Meteor strike! Magnitude {magnitude} at ({x}, {y}) with radius {radius}")
    return True