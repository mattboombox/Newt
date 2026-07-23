import random

from .critter import CARDINAL_DIRECTIONS, Critter


class SandWorm(Critter):
    """A multi-tile desert burrower that grows like a classic snake."""

    ALLOWED_TERRAINS = {"sand"}
    START_LENGTH = 2
    MAX_LENGTH = 13
    TILES_PER_GROWTH = 4000
    MOVE_COOLDOWN = 0.18
    TURN_CHANCE = 0.25

    def __init__(self, x, y):
        super().__init__(
            x,
            y,
            color=(120, 78, 38),
            allowed_terrains=SandWorm.ALLOWED_TERRAINS,
            move_cooldown=SandWorm.MOVE_COOLDOWN,
        )
        self.body_positions = [(x, y)]
        self.direction = None
        self.sand_tiles_traversed = 0
        self.tiles_since_growth = 0

    def get_occupied_positions(self):
        return tuple(self.body_positions)

    def on_spawn(self, game, allow_incompatible=False):
        """Claim a tail tile so a player-spawned worm begins at length two."""
        world = game.world
        candidates = []

        for dx, dy in CARDINAL_DIRECTIONS:
            tail_x = (self.x - dx) % world.cols
            tail_y = self.y - dy
            tail = world.get_tile(tail_x, tail_y)
            if (
                tail is not None
                and (allow_incompatible or tail.terrain == "sand")
                and tail.critter is None
            ):
                candidates.append(((dx, dy), (tail_x, tail_y)))

        if not candidates:
            return False

        self.direction, tail_position = random.choice(candidates)
        self.body_positions = [(self.x, self.y), tail_position]
        world.get_tile(*tail_position).critter = self
        self.set_behavior("burrow")
        return True

    def get_direction_between(self, world, tail_position, head_position):
        tail_x, tail_y = tail_position
        for dx, dy in CARDINAL_DIRECTIONS:
            if ((tail_x + dx) % world.cols, tail_y + dy) == head_position:
                return dx, dy
        return None

    def claim_body(self, world, positions):
        old_positions = set(self.body_positions)
        new_positions = set(positions)

        for x, y in old_positions - new_positions:
            tile = world.get_tile(x, y)
            if tile is not None and tile.critter is self:
                tile.critter = None

        self.body_positions = list(positions)
        self.x, self.y = self.body_positions[0]
        for x, y in self.body_positions:
            world.get_tile(x, y).critter = self

    def get_valid_moves_from(self, world, x, y):
        occupied_positions = set(self.body_positions)
        valid_moves = []

        for dx, dy in CARDINAL_DIRECTIONS:
            nx = (x + dx) % world.cols
            ny = y + dy
            tile = world.get_tile(nx, ny)
            if (
                tile is None
                or tile.terrain != "sand"
                or (nx, ny) in occupied_positions
                or tile.critter is not None
            ):
                continue
            valid_moves.append((dx, dy, nx, ny))

        return valid_moves

    def get_valid_moves(self, world):
        return self.get_valid_moves_from(world, self.x, self.y)

    def reverse_if_tail_can_escape(self, world):
        tail_x, tail_y = self.body_positions[-1]
        if not self.get_valid_moves_from(world, tail_x, tail_y):
            return False

        self.body_positions.reverse()
        self.x, self.y = self.body_positions[0]
        self.direction = self.get_direction_between(
            world,
            self.body_positions[1],
            self.body_positions[0],
        )
        self.set_behavior("reverse")
        return True

    def choose_move(self, world):
        valid_moves = self.get_valid_moves(world)
        if not valid_moves:
            return None

        forward_move = next(
            (
                move
                for move in valid_moves
                if move[:2] == self.direction
            ),
            None,
        )
        if forward_move is not None and random.random() >= self.TURN_CHANCE:
            return forward_move

        return random.choice(valid_moves)

    def create_split_child(self, game, body_positions):
        world = game.world
        if len(body_positions) != self.START_LENGTH:
            return None

        for x, y in body_positions:
            tile = world.get_tile(x, y)
            if tile is None or tile.terrain != "sand" or tile.critter is not None:
                return None

        child = SandWorm(*body_positions[0])
        child.body_positions = list(body_positions)
        child.direction = child.get_direction_between(
            world,
            body_positions[1],
            body_positions[0],
        )
        child.set_behavior("burrow")
        for x, y in body_positions:
            world.get_tile(x, y).critter = child
        game.critters.append(child)
        return child

    def split(self, game):
        from entity_cleanup import remove_critter

        old_body = list(self.body_positions)
        remove_critter(game, self, "it completed its final growth cycle")

        children = [
            self.create_split_child(game, old_body[:self.START_LENGTH]),
            self.create_split_child(
                game,
                list(reversed(old_body[-self.START_LENGTH:])),
            ),
        ]
        spawned_children = [child for child in children if child is not None]
        print(
            f"Sand Worm {self.id} split into "
            f"{len(spawned_children)} young worms."
        )
        return spawned_children

    def move_and_feed(self, game):
        move = self.choose_move(game.world)
        if move is None and self.reverse_if_tail_can_escape(game.world):
            move = self.choose_move(game.world)

        if move is None:
            self.set_behavior("blocked")
            return False

        dx, dy, nx, ny = move
        self.direction = (dx, dy)
        self.sand_tiles_traversed += 1
        self.tiles_since_growth += 1
        growth_due = self.tiles_since_growth >= self.TILES_PER_GROWTH
        was_at_max_length = len(self.body_positions) >= self.MAX_LENGTH

        if growth_due:
            self.tiles_since_growth = 0

        new_body = [(nx, ny)] + self.body_positions
        if not growth_due or was_at_max_length:
            new_body.pop()

        self.claim_body(game.world, new_body)
        self.set_behavior("grow" if growth_due else "burrow")

        if growth_due and was_at_max_length:
            self.split(game)

        return True

    def update(self, game, dt):
        body_is_on_sand = True
        for x, y in self.body_positions:
            tile = game.world.get_tile(x, y)
            if tile is None or tile.critter is not self:
                from entity_cleanup import remove_critter

                remove_critter(game, self, "part of its body was destroyed")
                return
            if tile.terrain != "sand":
                body_is_on_sand = False

        if not body_is_on_sand:
            from entity_cleanup import remove_critter

            remove_critter(game, self, "it was stranded away from sand")
            return

        self.needs_habitat_relocation = False

        self.move_timer += dt
        if self.move_timer < self.move_cooldown:
            return

        self.move_timer = 0.0
        self.move_and_feed(game)
