import random

from entity_cleanup import clear_tile_occupants
from lake import convert_landlocked_ocean_to_lake
from tectonics import sync_volcano_at_tile


class ImpactWave:
    def __init__(self, x, y, core_radius, burn_radius, impact_type, interval=0.08):
        self.x = x
        self.y = y
        self.core_radius = core_radius
        self.burn_radius = burn_radius
        self.impact_type = impact_type
        self.interval = interval
        self.timer = 0.0
        self.current_radius = -1
        self.current_ring = set()

        self.target_terrain = "lake" if impact_type == "comet" else "lava"
        self.protected_terrain = {
            "ocean",
            "trench",
            "shallows",
            "lake",
            "mountain",
            "active_volcano",
            "dormant_volcano",
            "lava",
        }

    def update(self, game, dt):
        self.timer += dt
        if self.timer < self.interval:
            return

        self.timer = 0.0
        self.advance(game)

    def advance(self, game):
        self.current_radius += 1
        self.current_ring = set()

        previous_radius_sq = (self.current_radius - 1) * (self.current_radius - 1)
        current_radius_sq = self.current_radius * self.current_radius

        for dx in range(-self.current_radius, self.current_radius + 1):
            for dy in range(-self.current_radius, self.current_radius + 1):
                distance_sq = dx * dx + dy * dy

                if self.current_radius == 0:
                    if distance_sq != 0:
                        continue
                elif distance_sq > current_radius_sq or distance_sq <= previous_radius_sq:
                    continue

                tile = game.world.get_tile(self.x + dx, self.y + dy)
                if tile is None:
                    continue

                self.current_ring.add((tile.x, tile.y))
                clear_tile_occupants(game, tile, f"it was hit by a {self.impact_type}")

                if distance_sq <= self.core_radius * self.core_radius:
                    self.set_tile_terrain(game, tile, self.target_terrain)
                elif distance_sq <= self.burn_radius * self.burn_radius:
                    if tile.terrain not in self.protected_terrain:
                        self.set_tile_terrain(game, tile, "stone")

        if self.current_radius >= self.burn_radius:
            self.finish(game)

    def set_tile_terrain(self, game, tile, terrain_name):
        sync_volcano_at_tile(game, tile, terrain_name)
        tile.set_terrain(terrain_name)

    def finish(self, game):
        center_tile = game.world.get_tile(self.x, self.y)
        if center_tile is not None:
            self.set_tile_terrain(game, center_tile, "mountain")

        convert_landlocked_ocean_to_lake(game.world)
        self.remove_from_game(game)

    def remove_from_game(self, game):
        self.current_ring.clear()

        if self in game.impact_waves:
            game.impact_waves.remove(self)


def trigger_impact_event(game, x=None, y=None, min_radius=2, max_radius=4, impact_type=None):
    world = game.world
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

    if impact_type is None:
        impact_type = random.choice(["meteor", "comet"])

    wave = ImpactWave(
        x,
        y,
        core_radius=radius,
        burn_radius=radius + 3,
        impact_type=impact_type,
    )
    game.impact_waves.append(wave)
    wave.advance(game)

    print(f"{impact_type.capitalize()} impact! Magnitude {magnitude} at ({x}, {y}) with radius {radius}")
    return True
