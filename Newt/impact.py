import random

def trigger_impact_event(world, x=None, y=None, min_radius=2, max_radius=4):
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

    is_comet = random.random() < 0.5
    impact_name = "Comet" if is_comet else "Meteor"

    for dx in range(-outer_radius, outer_radius + 1):
        for dy in range(-outer_radius, outer_radius + 1):
            tile = world.get_tile(x + dx, y + dy)
            if tile is None:
                continue

            distance_sq = dx * dx + dy * dy

            if is_comet:
                # COMET (water-based)
                if distance_sq <= radius * radius:
                    tile.set_terrain("ocean")
                elif distance_sq <= outer_radius * outer_radius:
                    if random.random() < 0.10:
                        tile.set_terrain("lake")
            else:
                # METEOR (lava-based)
                if distance_sq <= radius * radius:
                    tile.set_terrain("lava")
                elif distance_sq <= outer_radius * outer_radius:
                    if random.random() < 0.18:
                        tile.set_terrain("lava")

    # Always leave a mountain at the center
    center_tile.set_terrain("mountain")

    print(f"{impact_name} impact! Magnitude {magnitude} at ({x}, {y}) with radius {radius}")
    return True