import random

from entity_cleanup import clear_tile_occupants

DIRECTIONS = [
    (1, 0),    # east
    (1, 1),    # southeast
    (0, 1),    # south
    (-1, 1),   # southwest
    (-1, 0),   # west
    (-1, -1),  # northwest
    (0, -1),   # north
    (1, -1),   # northeast
]

UPLIFT_BLOCKED_TERRAINS = {
    "active_volcano",
    "dormant_volcano",
    "lava",
    "lake",
}

TRENCH_CARVABLE_TERRAINS = {"ocean", "trench"}


class Volcano:
    def __init__(self, x, y, state="active"):
        self.x = x
        self.y = y
        self.state = state

        self.eruption_timer = 0.0
        self.eruption_interval = 1.0

        self.dormancy_timer = 0.0
        self.dormancy_interval = 36.0

        self.reawaken_chance = 0.02
        self.extinction_chance = 0.08
        self.chain_chance = 0.75
        self.lava_radius = 3

    def update(self, game, dt):
        if self.state == "active":
            self.eruption_timer += dt
            self.dormancy_timer += dt

            if self.eruption_timer >= self.eruption_interval:
                self.eruption_timer = 0.0
                self.erupt(game)

            if self.dormancy_timer >= self.dormancy_interval:
                self.go_dormant(game)

        elif self.state == "dormant":
            if random.random() < self.reawaken_chance * dt:
                self.reawaken(game)
                return

            if random.random() < self.extinction_chance * dt:
                self.go_extinct(game)
                return

    def erupt(self, game):
        world = game.world
        center_tile = world.get_tile(self.x, self.y)
        if center_tile is not None:
            center_tile.set_terrain("active_volcano")

        effective_radius = self.lava_radius + 1
        valid_tiles = []

        for dx in range(-effective_radius, effective_radius + 1):
            for dy in range(-effective_radius, effective_radius + 1):
                if dx == 0 and dy == 0:
                    continue

                distance_sq = dx * dx + dy * dy
                if distance_sq > effective_radius * effective_radius:
                    continue

                tile = world.get_tile(self.x + dx, self.y + dy)
                if tile is None:
                    continue

                if tile.terrain in ("mountain", "active_volcano", "dormant_volcano"):
                    continue

                valid_tiles.append(tile)

        if not valid_tiles:
            return

        shots = min(len(valid_tiles), random.randint(1, 3))
        for tile in random.sample(valid_tiles, shots):
            clear_tile_occupants(game, tile, "lava erupted from a volcano")
            tile.set_terrain("lava")

    def go_dormant(self, game):
        self.state = "dormant"
        self.eruption_timer = 0.0
        self.dormancy_timer = 0.0

        center_tile = game.world.get_tile(self.x, self.y)
        if center_tile is not None:
            center_tile.set_terrain("dormant_volcano")

    def reawaken(self, game):
        self.state = "active"
        self.eruption_timer = 0.0
        self.dormancy_timer = 0.0

        center_tile = game.world.get_tile(self.x, self.y)
        if center_tile is not None:
            center_tile.set_terrain("active_volcano")

    def go_extinct(self, game):
        center_tile = game.world.get_tile(self.x, self.y)
        if center_tile is not None:
            center_tile.set_terrain("mountain")

        self.try_chain_spawn(game)
        self.remove_from_game(game)

    def try_chain_spawn(self, game):
        neighbors = game.world.get_neighbors_all(self.x, self.y)
        random.shuffle(neighbors)

        valid_tiles = []
        for tile in neighbors:
            if tile.terrain not in ("active_volcano", "dormant_volcano"):
                valid_tiles.append(tile)

        if not valid_tiles:
            return

        if random.random() < self.chain_chance:
            tile = random.choice(valid_tiles)
            self.spawn_new_volcano(game, tile.x, tile.y)

    def spawn_new_volcano(self, game, x, y):
        tile = game.world.get_tile(x, y)
        if tile is None:
            return

        existing = get_volcano_at(game, x, y)
        if existing is not None:
            existing.state = "active"
            tile.set_terrain("active_volcano")
            return

        tile.set_terrain("active_volcano")
        game.volcanoes.append(Volcano(x, y, state="active"))

    def remove_from_game(self, game):
        if self in game.volcanoes:
            game.volcanoes.remove(self)


def get_volcano_at(game, x, y):
    for volcano in game.volcanoes:
        if volcano.x == x and volcano.y == y:
            return volcano
    return None


def remove_volcano_at(game, x, y):
    volcano = get_volcano_at(game, x, y)
    if volcano is not None:
        game.volcanoes.remove(volcano)
        return True
    return False


def sync_volcano_at_tile(game, tile, terrain_name):
    # If painting over an existing volcano with something non-volcanic, remove it
    if terrain_name not in ("active_volcano", "dormant_volcano"):
        remove_volcano_at(game, tile.x, tile.y)
        return

    # If painting a volcano terrain, make sure a volcano object exists
    existing = get_volcano_at(game, tile.x, tile.y)

    if existing is None:
        state = "active" if terrain_name == "active_volcano" else "dormant"
        game.volcanoes.append(Volcano(tile.x, tile.y, state=state))
    else:
        existing.state = "active" if terrain_name == "active_volcano" else "dormant"


def spawn_dormant_volcano(game, x, y):
    tile = game.world.get_tile(x, y)
    if tile is None:
        return False

    if tile.terrain in ("active_volcano", "dormant_volcano", "lava", "lake"):
        return False

    existing = get_volcano_at(game, x, y)
    if existing is not None:
        existing.state = "dormant"
        tile.set_terrain("dormant_volcano")
        return True

    tile.set_terrain("dormant_volcano")
    game.volcanoes.append(Volcano(x, y, state="dormant"))
    return True


def choose_uplift_profile():
    roll = random.random()

    if roll < 0.45:
        return {
            "length": random.randint(8, 18),
            "land_half_width": 0,
        }

    if roll < 0.80:
        return {
            "length": random.randint(12, 22),
            "land_half_width": 1,
        }

    return {
        "length": random.randint(16, 28),
        "land_half_width": random.choice([1, 2]),
    }


def choose_uplift_chain_count():
    roll = random.random()
    if roll < 0.05:
        return 3
    if roll < 0.30:
        return 2
    return 1


def choose_trench_profile():
    roll = random.random()

    if roll < 0.55:
        return {
            "length": random.randint(8, 14),
        }

    if roll < 0.85:
        return {
            "length": random.randint(10, 18),
        }

    return {
        "length": random.randint(14, 22),
    }


def raise_uplift_spine_tile(game, x, y):
    tile = game.world.get_tile(x, y)
    if tile is None or tile.terrain in UPLIFT_BLOCKED_TERRAINS:
        return

    tile.set_terrain("mountain")

    # Chance to seed stone as a mountain pass.
    if random.random() < 0.05:
        tile.set_terrain("stone")

    # Low chance to seed a dormant volcano inside the chain.
    if random.random() < 0.08:
        spawn_dormant_volcano(game, x, y)


def raise_uplift_shoulder_tile(game, x, y, terrain_name):
    tile = game.world.get_tile(x, y)
    if tile is None or tile.terrain in UPLIFT_BLOCKED_TERRAINS:
        return

    tile.set_terrain(terrain_name)


def widen_uplift_chain(game, x, y, dir_index, land_half_width):
    if land_half_width <= 0:
        return

    left_dx, left_dy = DIRECTIONS[(dir_index - 2) % len(DIRECTIONS)]
    right_dx, right_dy = DIRECTIONS[(dir_index + 2) % len(DIRECTIONS)]
    side_vectors = ((left_dx, left_dy), (right_dx, right_dy))

    for side_dx, side_dy in side_vectors:
        for offset in range(1, land_half_width + 1):
            tx = x + side_dx * offset
            ty = y + side_dy * offset
            raise_uplift_shoulder_tile(game, tx, ty, "stone")

            # Fill occasional diagonal gaps so wider chains feel contiguous.
            if random.random() < 0.22:
                fan_tx = tx + DIRECTIONS[dir_index][0]
                fan_ty = ty + DIRECTIONS[dir_index][1]
                raise_uplift_shoulder_tile(game, fan_tx, fan_ty, "stone")


def generate_uplift_chain(game, start_x, start_y, length=None):
    world = game.world

    profile = choose_uplift_profile()

    if length is None:
        length = profile["length"]

    land_half_width = profile["land_half_width"]

    x = start_x
    y = start_y
    print(
        f"Generating uplift chain starting at ({x}, {y}) with length {length}, "
        f"mountain width 1, "
        f"land width {1 + land_half_width * 2}"
    )

    main_dir_index = random.randint(0, len(DIRECTIONS) - 1)

    for _ in range(length):
        tile = world.get_tile(x, y)
        if tile is None:
            break

        if tile.terrain not in UPLIFT_BLOCKED_TERRAINS:
            raise_uplift_spine_tile(game, x, y)
            widen_uplift_chain(
                game,
                x,
                y,
                main_dir_index,
                land_half_width,
            )

        scatter_stone_around_tile(world, x, y)

        roll = random.random()
        if roll < 0.65:
            dir_index = main_dir_index
        elif roll < 0.825:
            dir_index = (main_dir_index - 1) % len(DIRECTIONS)
        else:
            dir_index = (main_dir_index + 1) % len(DIRECTIONS)

        dx, dy = DIRECTIONS[dir_index]
        x += dx
        y += dy

        if random.random() < 0.25:
            turn = random.choice([-1, 1])
            main_dir_index = (main_dir_index + turn) % len(DIRECTIONS)


def trigger_uplift_event(game, start_x, start_y):
    chain_count = choose_uplift_chain_count()
    if chain_count > 1:
        print(f"Generating {chain_count} uplift chains from ({start_x}, {start_y})")

    for _ in range(chain_count):
        generate_uplift_chain(game, start_x, start_y)


def carve_trench_tile(world, x, y):
    tile = world.get_tile(x, y)
    if tile is None or tile.terrain not in TRENCH_CARVABLE_TERRAINS:
        return False

    if world.is_adjacent_to_terrain(x, y, {"shallows"}):
        tile.set_terrain("ocean")
        return False

    tile.set_terrain("trench")
    return True


def generate_trench_chain(game, start_x, start_y, length=None):
    world = game.world
    start_tile = world.get_tile(start_x, start_y)
    if start_tile is None or start_tile.terrain not in TRENCH_CARVABLE_TERRAINS:
        return False

    profile = choose_trench_profile()
    if length is None:
        length = profile["length"]

    x = start_x
    y = start_y
    print(f"Generating trench chain starting at ({x}, {y}) with length {length}")

    main_dir_index = random.randint(0, len(DIRECTIONS) - 1)
    carved_any = False

    for _ in range(length):
        if not carve_trench_tile(world, x, y):
            break

        carved_any = True

        roll = random.random()
        if roll < 0.65:
            dir_index = main_dir_index
        elif roll < 0.825:
            dir_index = (main_dir_index - 1) % len(DIRECTIONS)
        else:
            dir_index = (main_dir_index + 1) % len(DIRECTIONS)

        dx, dy = DIRECTIONS[dir_index]
        next_x = x + dx
        next_y = y + dy
        next_tile = world.get_tile(next_x, next_y)
        if next_tile is None or next_tile.terrain not in TRENCH_CARVABLE_TERRAINS:
            break

        x = next_x
        y = next_y

        if random.random() < 0.25:
            turn = random.choice([-1, 1])
            main_dir_index = (main_dir_index + turn) % len(DIRECTIONS)

    return carved_any


def trigger_trench_event(game, start_x, start_y):
    chain_count = choose_uplift_chain_count()
    if chain_count > 1:
        print(f"Generating {chain_count} trench chains from ({start_x}, {start_y})")

    carved_any = False
    for _ in range(chain_count):
        if generate_trench_chain(game, start_x, start_y):
            carved_any = True

    return carved_any


def scatter_stone_around_tile(world, x, y):
    # Weighted so radius 4 and 5 are the most common
    scatter_radius = random.choices([2, 3, 4, 5, 6], weights=[1, 2, 3, 3, 1])[0]

    for dx in range(-scatter_radius, scatter_radius + 1):
        for dy in range(-scatter_radius, scatter_radius + 1):
            if dx == 0 and dy == 0:
                continue

            neighbor = world.get_tile(x + dx, y + dy)
            if neighbor is None:
                continue

            if neighbor.terrain in (
                "active_volcano",
                "dormant_volcano",
                "lava",
                "mountain",
                "lake",
                "stone",
            ):
                continue

            distance = max(abs(dx), abs(dy))

            if distance == 1:
                chance = 0.50
            elif distance == 2:
                chance = 0.35
            elif distance == 3:
                chance = 0.20
            else:
                chance = 0.10

            if random.random() < chance:
                neighbor.set_terrain("stone")
