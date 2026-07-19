import random
from collections import deque


NON_ARCTIC_LAND_TERRAINS = {
    "beach",
    "grass",
    "sand",
    "snow",
    "stone",
    "ice_sheet",
}

CARDINAL_DIRECTIONS = [
    (1, 0), (-1, 0),
    (0, 1), (0, -1),
]


class Critter:
    _next_id = 1
    DYING_INTERVAL = 12.0
    REPRODUCTION_MEAL_THRESHOLD = 5
    REPRODUCTION_MEAL_VALUE = 1
    FLEE_DETECTION_RADIUS = 0

    def __init__(
        self,
        x,
        y,
        color=(255, 200, 40),
        allowed_terrains=None,
        required_tags=None,
        sprite=None,
        move_cooldown=0.25,
    ):
        self.id = Critter._next_id
        Critter._next_id += 1

        self.x = x
        self.y = y
        self.color = color
        self.sprite = sprite

        self.move_cooldown = move_cooldown
        self.move_timer = 0.0
        self.current_behavior = "wander"
        self.hunger_interval = None
        self.starvation_interval = None
        self.hunger_timer = None
        self.starvation_timer = None
        self.dying_timer = None
        self.is_hungry = False
        self.meals_eaten = 0

        self.allowed_terrains = allowed_terrains
        self.required_tags = required_tags or set()

    def set_behavior(self, behavior_name):
        self.current_behavior = behavior_name

    def configure_hunger(self, hunger_interval, starvation_interval):
        self.hunger_interval = hunger_interval
        self.starvation_interval = starvation_interval
        self.reset_hunger()

    def reset_hunger(self):
        if self.hunger_interval is None:
            return

        self.is_hungry = False
        self.hunger_timer = self.hunger_interval
        self.starvation_timer = self.starvation_interval

    def get_reproduction_meal_value(self, prey=None):
        if prey is None:
            return 1
        return getattr(prey, "REPRODUCTION_MEAL_VALUE", 1)

    def handle_successful_meal(self, game, meal_points=None):
        if meal_points is None:
            meal_points = 1

        self.meals_eaten += meal_points
        self.set_behavior("eat")
        self.reset_hunger()

        if self.meals_eaten < self.REPRODUCTION_MEAL_THRESHOLD:
            return None

        offspring = self.try_reproduce(game.world)
        if offspring is None:
            return None

        game.critters.append(offspring)
        self.meals_eaten = 0
        return offspring

    def update(self, game, dt):
        if self.current_behavior == "dying":
            self.update_dying(game, dt)
            return

        if not self.update_hunger(game, dt):
            return

        self.move_timer += dt
        if self.move_timer < self.move_cooldown:
            return

        self.move_timer = 0.0
        if self.try_flee_from_predators(game.world, game):
            return

        if self.try_scavenge_nearby_corpse(game):
            return

        if self.is_hungry:
            self.take_hungry_action(game)
        else:
            self.try_wander(game.world, game)

    def update_hunger(self, game, dt):
        if self.hunger_interval is None:
            return True

        if self.is_hungry:
            self.starvation_timer -= dt
            if self.starvation_timer <= 0:
                self.start_dying()
                return False
            return True

        self.hunger_timer -= dt
        if self.hunger_timer <= 0:
            self.is_hungry = True
            self.starvation_timer = self.starvation_interval
            self.set_behavior("hungry")

        return True

    def start_dying(self):
        self.is_hungry = False
        self.hunger_timer = None
        self.starvation_timer = None
        self.dying_timer = self.DYING_INTERVAL
        self.move_timer = 0.0
        self.set_behavior("dying")

    def update_dying(self, game, dt):
        if self.dying_timer is None:
            self.dying_timer = self.DYING_INTERVAL

        self.dying_timer -= dt
        if self.dying_timer > 0:
            return

        self.finish_dying(game)

    def finish_dying(self, game):
        from entity_cleanup import remove_critter

        tile = game.world.get_tile(self.x, self.y)
        removed = remove_critter(game, self, "its corpse decayed away")
        if removed:
            self.spawn_death_remains(game, tile)

    def spawn_death_remains(self, game, tile):
        return False

    def try_spawn_grass_remains(self, game, tile):
        if tile is None or tile.critter is not None or tile.terrain != "sand":
            return False

        from life import grow_tile

        return grow_tile(game.world, tile)

    def try_spawn_plankton_remains(self, game, tile):
        if tile is None or tile.critter is not None or tile.terrain not in Plankton.ALLOWED_TERRAINS:
            return False

        plankton = Plankton(tile.x, tile.y)
        tile.critter = plankton
        game.critters.append(plankton)
        return True

    def is_habitable_tile(self, tile):
        if tile is None:
            return False

        if self.allowed_terrains is not None and tile.terrain not in self.allowed_terrains:
            return False

        for tag in self.required_tags:
            if not tile.has_tag(tag):
                return False

        return True

    def can_displace_critter(self, critter):
        return False

    def get_flee_predator_types(self):
        return ()

    def get_flee_detection_radius(self):
        return self.FLEE_DETECTION_RADIUS

    def should_attempt_shove_displacement(self, critter):
        return True

    def get_displacement_meal_value(self, critter):
        return None

    def get_scavenge_prey_types(self):
        return ()

    def get_scavenge_predator_name(self):
        return type(self).__name__

    def try_scavenge_nearby_corpse(self, game):
        prey_types = self.get_scavenge_prey_types()
        if not prey_types:
            return False

        if not isinstance(prey_types, tuple):
            prey_types = (prey_types,)

        destinations = self.get_neighbor_positions(game.world, self.x, self.y)
        random.shuffle(destinations)

        for nx, ny in destinations:
            tile = game.world.get_tile(nx, ny)
            if tile is None or not self.is_habitable_tile(tile):
                continue

            prey = tile.critter
            if (
                prey is None
                or prey.current_behavior != "dying"
                or not isinstance(prey, prey_types)
            ):
                continue

            self.remove_other_critter(game, prey, self.get_scavenge_predator_name())
            self.move_to(game.world, nx, ny, game)
            self.handle_successful_meal(game, self.get_reproduction_meal_value(prey))
            return True

        return False

    def remove_other_critter(self, game, critter, predator_name=None):
        from entity_cleanup import remove_critter

        if predator_name is None:
            predator_name = type(self).__name__

        remove_critter(game, critter, f"it was eaten by {predator_name} {self.id}")

    def try_relocate_displaced_critter(self, world, critter):
        destinations = critter.get_neighbor_positions(world, critter.x, critter.y)
        random.shuffle(destinations)

        for nx, ny in destinations:
            tile = world.get_tile(nx, ny)
            if tile is None or tile.critter is not None:
                continue

            if not critter.is_habitable_tile(tile):
                continue

            current_tile = world.get_tile(critter.x, critter.y)
            if current_tile is not None and current_tile.critter is critter:
                current_tile.critter = None

            critter.x = nx
            critter.y = ny
            tile.critter = critter
            return True

        return False

    def displace_critter(self, game, world, critter):
        if self.should_attempt_shove_displacement(critter) and self.try_relocate_displaced_critter(world, critter):
            return

        self.remove_other_critter(game, critter)
        meal_points = self.get_displacement_meal_value(critter)
        if meal_points is not None:
            self.handle_successful_meal(game, meal_points)

    def can_enter_tile(self, tile):
        if tile is None:
            return False

        if tile.critter is not None and not self.can_displace_critter(tile.critter):
            return False

        return self.is_habitable_tile(tile)

    def move_to(self, world, nx, ny, game=None):
        tile = world.get_tile(nx, ny)
        if not self.can_enter_tile(tile):
            return False

        old_tile = world.get_tile(self.x, self.y)

        if tile.critter is not None:
            if game is None:
                return False

            displaced_critter = tile.critter
            if old_tile is not None:
                old_tile.critter = None
            self.displace_critter(game, world, displaced_critter)

        elif old_tile is not None:
            old_tile.critter = None

        self.x = nx
        self.y = ny
        tile.critter = self
        return True

    def try_wander(self, world, game=None):
        self.set_behavior("wander")
        directions = CARDINAL_DIRECTIONS[:]
        random.shuffle(directions)

        for dx, dy in directions:
            nx = (self.x + dx) % world.cols
            ny = self.y + dy

            tile = world.get_tile(nx, ny)
            if self.can_enter_tile(tile):
                self.move_to(world, nx, ny, game)
                return

    def try_reproduce(self, world):
        directions = CARDINAL_DIRECTIONS[:]
        random.shuffle(directions)

        for dx, dy in directions:
            nx = (self.x + dx) % world.cols
            ny = self.y + dy

            tile = world.get_tile(nx, ny)
            if not self.can_enter_tile(tile):
                continue

            offspring = type(self)(nx, ny)
            tile.critter = offspring
            self.set_behavior("reproduce")
            return offspring

        return None

    def get_neighbor_positions(self, world, x, y):
        neighbors = []
        for dx, dy in CARDINAL_DIRECTIONS:
            nx = (x + dx) % world.cols
            ny = y + dy
            if 0 <= ny < world.rows:
                neighbors.append((nx, ny))
        return neighbors

    def get_tile_distance(self, world, x1, y1, x2, y2):
        dx = abs(x1 - x2)
        dx = min(dx, world.cols - dx)
        dy = abs(y1 - y2)
        return dx + dy

    def find_nearby_predators(self, world):
        predator_types = self.get_flee_predator_types()
        radius = self.get_flee_detection_radius()
        if not predator_types or radius <= 0:
            return []

        threats = []
        for dy in range(-radius, radius + 1):
            ny = self.y + dy
            if ny < 0 or ny >= world.rows:
                continue

            max_dx = radius - abs(dy)
            for dx in range(-max_dx, max_dx + 1):
                if dx == 0 and dy == 0:
                    continue

                nx = (self.x + dx) % world.cols
                tile = world.get_tile(nx, ny)
                if tile is None or tile.critter is None:
                    continue

                if tile.critter.current_behavior == "dying":
                    continue

                if isinstance(tile.critter, predator_types):
                    threats.append((nx, ny))

        return threats

    def get_flee_score(self, world, x, y, threats):
        distances = [self.get_tile_distance(world, x, y, tx, ty) for tx, ty in threats]
        return (min(distances), sum(distances))

    def try_flee_from_predators(self, world, game=None):
        threats = self.find_nearby_predators(world)
        if not threats:
            return False

        current_score = self.get_flee_score(world, self.x, self.y, threats)
        destinations = self.get_neighbor_positions(world, self.x, self.y)
        random.shuffle(destinations)

        best_move = None
        best_score = current_score
        for nx, ny in destinations:
            tile = world.get_tile(nx, ny)
            if not self.can_enter_tile(tile):
                continue

            score = self.get_flee_score(world, nx, ny, threats)
            if score > best_score:
                best_score = score
                best_move = (nx, ny)

        self.set_behavior("flee")
        if best_move is None:
            return True

        self.move_to(world, best_move[0], best_move[1], game)
        return True

    def reconstruct_path(self, came_from, end_pos):
        path = []
        current = end_pos
        while current is not None and came_from[current] is not None:
            path.append(current)
            current = came_from[current]
        path.reverse()
        return path

    def find_path_to_nearest_tile(self, world, target_predicate, allow_occupied_target=False):
        start_pos = (self.x, self.y)
        start_tile = world.get_tile(self.x, self.y)
        if start_tile is not None and target_predicate(start_tile):
            return []

        queue = deque([start_pos])
        came_from = {start_pos: None}

        while queue:
            x, y = queue.popleft()
            for nx, ny in self.get_neighbor_positions(world, x, y):
                next_pos = (nx, ny)
                if next_pos in came_from:
                    continue

                tile = world.get_tile(nx, ny)
                if tile is None:
                    continue

                if target_predicate(tile) and (allow_occupied_target or self.can_enter_tile(tile)):
                    came_from[next_pos] = (x, y)
                    return self.reconstruct_path(came_from, next_pos)

                if self.can_enter_tile(tile):
                    came_from[next_pos] = (x, y)
                    queue.append(next_pos)

        return None

    def forage_nearest_tile(self, game, current_tile_predicate, path_target_predicate, seek_behavior, on_feed):
        current_tile = game.world.get_tile(self.x, self.y)
        if current_tile is not None and current_tile_predicate(current_tile):
            on_feed(current_tile)
            return True

        path = self.find_path_to_nearest_tile(game.world, path_target_predicate)
        if not path:
            self.set_behavior("hungry")
            return False

        self.set_behavior(seek_behavior)
        next_x, next_y = path[0]
        self.move_to(game.world, next_x, next_y, game)

        current_tile = game.world.get_tile(self.x, self.y)
        if current_tile is not None and current_tile_predicate(current_tile):
            on_feed(current_tile)
            return True

        return False

    def hunt_nearest_prey(self, game, prey_types, predator_name=None):
        if not isinstance(prey_types, tuple):
            prey_types = (prey_types,)

        path = self.find_path_to_nearest_tile(
            game.world,
            lambda tile: (
                tile.critter is not None
                and isinstance(tile.critter, prey_types)
                and self.is_habitable_tile(tile)
            ),
            allow_occupied_target=True,
        )
        if not path:
            self.set_behavior("hungry")
            return False

        target_x, target_y = path[0]
        target_tile = game.world.get_tile(target_x, target_y)
        if target_tile is None:
            self.set_behavior("hungry")
            return False

        if target_tile.critter is not None and isinstance(target_tile.critter, prey_types):
            prey = target_tile.critter
            self.remove_other_critter(game, prey, predator_name)
            self.move_to(game.world, target_x, target_y, game)
            self.handle_successful_meal(game, self.get_reproduction_meal_value(prey))
            return True

        self.set_behavior("hunt")
        self.move_to(game.world, target_x, target_y, game)
        return True

    def take_hungry_action(self, game):
        self.set_behavior("hungry")


class Crab(Critter):
    ALLOWED_TERRAINS = {"beach", "shallows"}
    FEED_TERRAINS = {"shallows"}
    HUNGER_INTERVAL = 14.0
    STARVATION_INTERVAL = 10.0
    REPRODUCTION_MEAL_VALUE = 2

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(255, 80, 80),
            allowed_terrains=Crab.ALLOWED_TERRAINS,
            move_cooldown=0.30,
            sprite="crab"
        )
        self.configure_hunger(Crab.HUNGER_INTERVAL, Crab.STARVATION_INTERVAL)

    def can_displace_critter(self, critter):
        return isinstance(critter, Plankton)

    def get_displacement_meal_value(self, critter):
        if isinstance(critter, Plankton):
            return self.get_reproduction_meal_value(critter)
        return None

    def take_hungry_action(self, game):
        self.forage_nearest_tile(
            game,
            lambda tile: tile.terrain in Crab.FEED_TERRAINS,
            lambda tile: tile.terrain in Crab.FEED_TERRAINS,
            "seek_shallows",
            lambda tile: self.handle_successful_meal(game),
        )

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_plankton_remains(game, tile)


class Plankton(Critter):
    ALLOWED_TERRAINS = {"ocean", "trench", "shallows"}
    HUNGER_INTERVAL = 15.0
    STARVATION_INTERVAL = 8.0
    REPRODUCTION_MEAL_VALUE = 1

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(160, 255, 180),
            allowed_terrains=Plankton.ALLOWED_TERRAINS,
            move_cooldown=4.20,
            sprite="plankton"
        )
        self.configure_hunger(Plankton.HUNGER_INTERVAL, Plankton.STARVATION_INTERVAL)

    def take_hungry_action(self, game):
        self.handle_successful_meal(game)

    def try_reproduce(self, world):
        for nx, ny in self.get_neighbor_positions(world, self.x, self.y):
            neighbor_tile = world.get_tile(nx, ny)
            if neighbor_tile is not None and isinstance(neighbor_tile.critter, Plankton):
                return None

        return super().try_reproduce(world)


class Fish(Critter):
    ALLOWED_TERRAINS = {"ocean", "shallows", "lake"}
    HUNGER_INTERVAL = 40.0
    STARVATION_INTERVAL = 40.0
    REPRODUCTION_MEAL_THRESHOLD = 8
    REPRODUCTION_MEAL_VALUE = 2
    FLEE_DETECTION_RADIUS = 4

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(80, 180, 255),
            allowed_terrains=Fish.ALLOWED_TERRAINS,
            move_cooldown=0.18,
            sprite="fish"
        )
        self.configure_hunger(Fish.HUNGER_INTERVAL, Fish.STARVATION_INTERVAL)

    def can_displace_critter(self, critter):
        return isinstance(critter, Plankton)

    def get_displacement_meal_value(self, critter):
        if isinstance(critter, Plankton):
            return self.get_reproduction_meal_value(critter)
        return None

    def get_flee_predator_types(self):
        return (Squid,)

    def take_hungry_action(self, game):
        self.hunt_nearest_prey(game, (Crab, Plankton), "Fish")

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_plankton_remains(game, tile)


class Squid(Critter):
    ALLOWED_TERRAINS = {"ocean", "trench", "shallows", "lake"}
    HUNGER_INTERVAL = 200.0
    STARVATION_INTERVAL = 120.0
    REPRODUCTION_MEAL_VALUE = 5

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(180, 120, 220),
            allowed_terrains=Squid.ALLOWED_TERRAINS,
            move_cooldown=0.32,
            sprite="squid"
        )
        self.configure_hunger(Squid.HUNGER_INTERVAL, Squid.STARVATION_INTERVAL)

    def can_displace_critter(self, critter):
        return isinstance(critter, (Plankton, Crab))

    def get_displacement_meal_value(self, critter):
        if isinstance(critter, Crab):
            return self.get_reproduction_meal_value(critter)
        return None

    def get_scavenge_prey_types(self):
        return (Fish, Crab)

    def get_scavenge_predator_name(self):
        return "Squid"

    def take_hungry_action(self, game):
        self.hunt_nearest_prey(game, (Fish, Crab), "Squid")

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_plankton_remains(game, tile)


class Whale(Critter):
    ALLOWED_TERRAINS = {"ocean", "trench", "shallows"}
    REPRODUCTION_MEAL_THRESHOLD = 30
    HUNGER_INTERVAL = 18.0
    STARVATION_INTERVAL = 90.0

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(110, 150, 190),
            allowed_terrains=Whale.ALLOWED_TERRAINS,
            move_cooldown=0.36,
            sprite="whale"
        )
        self.configure_hunger(Whale.HUNGER_INTERVAL, Whale.STARVATION_INTERVAL)

    def can_displace_critter(self, critter):
        return isinstance(critter, Plankton)

    def get_displacement_meal_value(self, critter):
        if isinstance(critter, Plankton):
            return self.get_reproduction_meal_value(critter)
        return None

    def take_hungry_action(self, game):
        self.hunt_nearest_prey(game, Plankton, "Whale")

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_plankton_remains(game, tile)


class SpermWhale(Critter):
    ALLOWED_TERRAINS = Whale.ALLOWED_TERRAINS
    REPRODUCTION_MEAL_THRESHOLD = Whale.REPRODUCTION_MEAL_THRESHOLD
    HUNGER_INTERVAL = Whale.HUNGER_INTERVAL
    STARVATION_INTERVAL = 220.0

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(95, 105, 125),
            allowed_terrains=SpermWhale.ALLOWED_TERRAINS,
            move_cooldown=0.36,
            sprite="sperm_whale"
        )
        self.configure_hunger(SpermWhale.HUNGER_INTERVAL, SpermWhale.STARVATION_INTERVAL)

    def can_displace_critter(self, critter):
        return isinstance(critter, Plankton)

    def get_scavenge_prey_types(self):
        return Squid

    def get_scavenge_predator_name(self):
        return "Sperm Whale"

    def take_hungry_action(self, game):
        self.hunt_nearest_prey(game, Squid, "Sperm Whale")

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_plankton_remains(game, tile)


class Deer(Critter):
    ALLOWED_TERRAINS = NON_ARCTIC_LAND_TERRAINS
    HUNGER_INTERVAL = 40.0
    STARVATION_INTERVAL = 40.0
    GRASS_CONSUME_CHANCE = 0.10
    REPRODUCTION_MEAL_VALUE = 5
    FLEE_DETECTION_RADIUS = 5

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(180, 140, 90),
            allowed_terrains=Deer.ALLOWED_TERRAINS,
            move_cooldown=0.18,
            sprite="deer"
        )
        self.configure_hunger(Deer.HUNGER_INTERVAL, Deer.STARVATION_INTERVAL)

    def get_flee_predator_types(self):
        return (Wolf,)

    def take_hungry_action(self, game):
        def eat_grass(tile):
            if random.random() < Deer.GRASS_CONSUME_CHANCE:
                tile.set_terrain("sand")
            self.handle_successful_meal(game)

        self.forage_nearest_tile(
            game,
            lambda tile: tile.terrain == "grass",
            lambda tile: tile.terrain == "grass" and tile.critter is None,
            "seek_grass",
            eat_grass,
        )

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)


class Wolf(Critter):
    ALLOWED_TERRAINS = NON_ARCTIC_LAND_TERRAINS
    REPRODUCTION_MEAL_THRESHOLD = 10
    HUNGER_INTERVAL = 200.0
    STARVATION_INTERVAL = 120.0

    def __init__(self, x, y):
        super().__init__(
            x, y,
            color=(160, 160, 160),
            allowed_terrains=Wolf.ALLOWED_TERRAINS,
            move_cooldown=0.32,
            sprite="wolf"
        )
        self.configure_hunger(Wolf.HUNGER_INTERVAL, Wolf.STARVATION_INTERVAL)

    def get_scavenge_prey_types(self):
        return (Crab, Deer)

    def get_scavenge_predator_name(self):
        return "Wolf"

    def take_hungry_action(self, game):
        self.hunt_nearest_prey(game, (Crab, Deer), "Wolf")

    def spawn_death_remains(self, game, tile):
        return self.try_spawn_grass_remains(game, tile)


CRITTER_TYPES = {
    "crab": Crab,
    "deer": Deer,
    "fish": Fish,
    "plankton": Plankton,
    "sperm_whale": SpermWhale,
    "squid": Squid,
    "whale": Whale,
    "wolf": Wolf,
}

CRITTER_ORDER = list(CRITTER_TYPES.keys())
