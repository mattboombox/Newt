import random
from collections import deque
from heapq import heappop, heappush


LAND_TERRAINS = {
    "beach",
    "grass",
    "sand",
    "snow",
    "stone",
    "ice_sheet",
    "shallows",
}

AQUATIC_TERRAINS = {"ocean", "lake", "shallows", "trench"}

CARDINAL_DIRECTIONS = [
    (1, 0), (-1, 0),
    (0, 1), (0, -1),
]


class Critter:
    _next_id = 1
    DYING_INTERVAL = 12.0
    COMBAT_CAPABLE = False
    COMBAT_POWER = 1
    MAX_COMBAT_HEALTH = 1
    PASSIVE_HEAL_INTERVAL = 18.0
    MEAL_HEAL_AMOUNT = 1
    DAMAGE_FLASH_DURATION = 0.45
    # Critters may displace occupants from lower levels. Nutrition follows
    # the same scale by default, with +1 keeping level-zero prey valuable.
    DISPLACEMENT_LEVEL = 1
    FOOD_VALUE = None
    REPRODUCTION_MEAL_THRESHOLD = 5
    FLEE_DETECTION_RADIUS = 0
    # Finite by default so new predator species cannot accidentally scan the
    # whole map.  Whales explicitly override this for global prey searches.
    HUNT_RANGE = 12
    SCAVENGE_RANGE = None
    FORAGE_RANGE = 12
    HUNT_PREY_TYPES = ()
    SCAVENGE_PREY_TYPES = ()
    PREDATOR_NAME = None
    REPRODUCTION_BLOCKS_SET_BEHAVIOR = False
    REPRODUCTION_BLOCKS_RESET_MEALS = False
    REQUIRES_LIQUID_TO_REPRODUCE = False

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
        self.configure_combat()
        self.home_building = None
        self.needs_habitat_relocation = False
        self.trapped_by_web = None

        self.allowed_terrains = allowed_terrains
        self.required_tags = required_tags or set()

    def set_behavior(self, behavior_name):
        self.current_behavior = behavior_name

    def get_occupied_positions(self):
        return ((self.x, self.y),)

    def occupies_position(self, x, y):
        return (x, y) in self.get_occupied_positions()

    def configure_hunger(self, hunger_interval, starvation_interval):
        self.hunger_interval = hunger_interval
        self.starvation_interval = starvation_interval
        self.reset_hunger()

    def configure_combat(self):
        self.combat_health = self.MAX_COMBAT_HEALTH
        self.passive_heal_timer = self.PASSIVE_HEAL_INTERVAL
        self.damage_flash_timer = 0.0
        self.retaliation_target = None

    def heal_combat_damage(self, amount):
        if not self.COMBAT_CAPABLE or amount <= 0:
            return 0

        old_health = self.combat_health
        self.combat_health = min(
            self.MAX_COMBAT_HEALTH,
            self.combat_health + amount,
        )
        return self.combat_health - old_health

    def heal_from_meal(self):
        return self.heal_combat_damage(self.MEAL_HEAL_AMOUNT)

    def update_combat_state(self, dt):
        self.damage_flash_timer = max(0.0, self.damage_flash_timer - dt)
        if (
            not self.COMBAT_CAPABLE
            or self.current_behavior == "dying"
            or self.combat_health >= self.MAX_COMBAT_HEALTH
        ):
            self.passive_heal_timer = self.PASSIVE_HEAL_INTERVAL
            return

        self.passive_heal_timer -= dt
        while (
            self.passive_heal_timer <= 0
            and self.combat_health < self.MAX_COMBAT_HEALTH
        ):
            self.combat_health += 1
            self.passive_heal_timer += self.PASSIVE_HEAL_INTERVAL

    def take_combat_damage(self, amount=1, attacker=None):
        if not self.COMBAT_CAPABLE or amount <= 0:
            return False

        self.combat_health = max(0, self.combat_health - amount)
        self.passive_heal_timer = self.PASSIVE_HEAL_INTERVAL
        self.damage_flash_timer = self.DAMAGE_FLASH_DURATION
        if attacker is not None and attacker is not self:
            self.retaliation_target = attacker
        return self.combat_health <= 0

    def set_home_building(self, building):
        if self.home_building is building:
            return

        if self.home_building is not None:
            self.home_building.remove_resident(self)

        self.home_building = building
        if building is not None:
            building.add_resident(self)

    def clear_home_building(self):
        self.set_home_building(None)

    def reset_hunger(self):
        if self.hunger_interval is None:
            return

        self.is_hungry = False
        self.hunger_timer = self.hunger_interval
        self.starvation_timer = self.starvation_interval

    def get_reproduction_meal_value(self, prey=None):
        if prey is None:
            return 1
        return prey.get_food_value() + getattr(prey, "carrying_food", 0)

    def get_food_value(self):
        if self.FOOD_VALUE is not None:
            return self.FOOD_VALUE
        return self.DISPLACEMENT_LEVEL + 1

    def handle_successful_meal(self, game, meal_points=None):
        if meal_points is None:
            meal_points = 1

        self.meals_eaten += meal_points
        self.set_behavior("eat")
        self.reset_hunger()
        self.heal_from_meal()

        if self.meals_eaten < self.REPRODUCTION_MEAL_THRESHOLD:
            return None

        offspring = self.complete_reproduction(game)
        return offspring

    def complete_reproduction(self, game):
        offspring = self.try_reproduce(game.world)
        if offspring is None:
            return None

        if random.random() < game.evolution_chance:
            from evolution import replace_with_evolved_offspring

            evolved_offspring = replace_with_evolved_offspring(self, offspring, game.world)
            if evolved_offspring is not None:
                offspring = evolved_offspring

        game.critters.append(offspring)
        self.meals_eaten = max(
            0,
            self.meals_eaten - self.REPRODUCTION_MEAL_THRESHOLD,
        )
        return offspring

    def fail_reproduction_attempt(self, reset_meals=False):
        if reset_meals:
            self.meals_eaten = 0
        self.set_behavior("reproduce")
        return None

    def update(self, game, dt):
        self.update_combat_state(dt)

        if self.current_behavior == "dying":
            self.update_dying(game, dt)
            return

        if self.trapped_by_web is not None:
            tile = game.world.get_tile(self.x, self.y)
            if tile is not None and tile.building is self.trapped_by_web and tile.critter is self:
                self.set_behavior("trapped")
                return
            self.trapped_by_web = None

        if not self.update_hunger(game, dt):
            return

        self.move_timer += dt
        if self.move_timer < self.move_cooldown:
            return

        self.move_timer = 0.0
        current_tile = game.world.get_tile(self.x, self.y)
        if (
            not self.is_habitable_tile(current_tile)
            and (
                self.needs_habitat_relocation
                or (current_tile is not None and current_tile.terrain == "shallows")
            )
        ):
            self.try_relocate_to_habitat(game)
            return

        self.needs_habitat_relocation = False
        if self.try_flee_from_predators(game.world, game):
            return

        if self.try_scavenge_nearby_corpse(game):
            return

        if self.try_handle_retaliation(game):
            return

        if self.try_handle_priority_behavior(game):
            return

        if self.try_scavenge_corpse(game):
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
                self.start_dying(game)
                return False
            return True

        self.hunger_timer -= dt
        if self.hunger_timer <= 0:
            self.is_hungry = True
            self.starvation_timer = self.starvation_interval
            self.set_behavior("hungry")

        return True

    def start_dying(self, game=None):
        self.is_hungry = False
        self.hunger_timer = None
        self.starvation_timer = None
        self.dying_timer = self.DYING_INTERVAL
        self.move_timer = 0.0
        self.set_behavior("dying")
        if game is not None and hasattr(game, "dying_critters"):
            game.dying_critters.add(self)

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
        from .plankton import Plankton

        if tile is None or tile.critter is not None or tile.terrain not in Plankton.ALLOWED_TERRAINS:
            return False

        plankton = Plankton(tile.x, tile.y)
        tile.critter = plankton
        game.critters.append(plankton)
        return True

    def get_meal_based_remains_chance(self):
        if self.meals_eaten <= 0 or self.REPRODUCTION_MEAL_THRESHOLD <= 0:
            return 0.0
        return min(1.0, self.meals_eaten / self.REPRODUCTION_MEAL_THRESHOLD)

    def try_spawn_meal_based_remains(self, spawn_remains):
        chance = self.get_meal_based_remains_chance()
        return chance > 0 and random.random() < chance and spawn_remains()

    def try_spawn_meal_based_plankton_remains(self, game, tile):
        return self.try_spawn_meal_based_remains(
            lambda: self.try_spawn_plankton_remains(game, tile),
        )

    def try_spawn_squid_egg_remains(self, game, tile):
        from .squid_egg import SquidEgg

        if tile is None or tile.critter is not None or tile.terrain not in SquidEgg.ALLOWED_TERRAINS:
            return False

        squid_egg = SquidEgg(tile.x, tile.y)
        tile.critter = squid_egg
        game.critters.append(squid_egg)
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
        return self.DISPLACEMENT_LEVEL > critter.DISPLACEMENT_LEVEL

    def get_flee_predator_types(self):
        return ()

    def get_flee_detection_radius(self):
        return self.FLEE_DETECTION_RADIUS

    def should_attempt_shove_displacement(self, critter):
        return True

    def should_remove_on_failed_displacement(self, critter):
        return True

    def get_displacement_meal_value(self, critter):
        return None

    def try_handle_priority_behavior(self, game):
        if (
            self.REQUIRES_LIQUID_TO_REPRODUCE
            and self.meals_eaten >= self.REPRODUCTION_MEAL_THRESHOLD
        ):
            return self.try_seek_reproduction_liquid(game)

        return False

    def is_near_liquid(self, world, x=None, y=None):
        if x is None:
            x = self.x
        if y is None:
            y = self.y

        tile = world.get_tile(x, y)
        if tile is not None and tile.has_tag("water"):
            return True

        return any(
            world.get_tile(nx, ny).has_tag("water")
            for nx, ny in self.get_neighbor_positions(world, x, y)
            if world.get_tile(nx, ny) is not None
        )

    def try_seek_reproduction_liquid(self, game):
        if self.is_near_liquid(game.world):
            self.complete_reproduction(game)
            return True

        path = self.find_path_to_nearest_tile(
            game.world,
            lambda tile: self.is_habitable_tile(tile)
            and self.is_near_liquid(game.world, tile.x, tile.y),
        )
        self.set_behavior("seek_liquid")
        if not path:
            return True

        next_x, next_y = path[0]
        self.move_to(game.world, next_x, next_y, game)
        return True

    def get_predator_name(self):
        return self.PREDATOR_NAME or type(self).__name__

    def get_hunt_prey_types(self):
        return self.HUNT_PREY_TYPES

    def get_scavenge_prey_types(self):
        return self.SCAVENGE_PREY_TYPES

    def get_scavenge_predator_name(self):
        return self.get_predator_name()

    def can_be_hunted_by(self, predator):
        return True

    def is_valid_hunt_prey(self, critter, prey_types):
        return (
            isinstance(critter, prey_types)
            and critter.current_behavior != "dying"
            and critter.can_be_hunted_by(self)
        )

    def is_valid_scavenge_prey(self, critter, prey_types):
        return isinstance(critter, prey_types)

    def get_hunt_range(self):
        return self.HUNT_RANGE

    def get_scavenge_range(self):
        if self.SCAVENGE_RANGE is not None:
            return self.SCAVENGE_RANGE

        hunt_range = self.get_hunt_range()
        if hunt_range is None:
            return None

        return hunt_range * 3

    def get_forage_range(self):
        return self.FORAGE_RANGE

    def get_reproduction_blocking_types(self):
        return ()

    def handle_blocked_reproduction(self):
        if self.REPRODUCTION_BLOCKS_SET_BEHAVIOR:
            self.set_behavior("reproduce")
        if self.REPRODUCTION_BLOCKS_RESET_MEALS:
            self.meals_eaten = 0

    def is_reproduction_blocked(self, world):
        blocking_types = self.get_reproduction_blocking_types()
        if not blocking_types:
            return False

        if not isinstance(blocking_types, tuple):
            blocking_types = (blocking_types,)

        for nx, ny in self.get_neighbor_positions(world, self.x, self.y):
            neighbor_tile = world.get_tile(nx, ny)
            if neighbor_tile is not None and isinstance(neighbor_tile.critter, blocking_types):
                return True

        return False

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
                or not self.is_valid_scavenge_prey(prey, prey_types)
            ):
                continue

            self.remove_other_critter(game, prey, self.get_scavenge_predator_name())
            self.move_to(game.world, nx, ny, game)
            self.handle_successful_meal(game, self.get_reproduction_meal_value(prey))
            return True

        return False

    def try_scavenge_corpse(self, game):
        prey_types = self.get_scavenge_prey_types()
        if not prey_types:
            return False

        dying_critters = getattr(game, "dying_critters", ())
        if not dying_critters:
            return False

        if not isinstance(prey_types, tuple):
            prey_types = (prey_types,)

        scavenge_range = self.get_scavenge_range()
        target_positions = set()
        for critter in dying_critters:
            if (
                critter.current_behavior != "dying"
                or not self.is_valid_scavenge_prey(critter, prey_types)
            ):
                continue

            for target_x, target_y in critter.get_occupied_positions():
                tile = game.world.get_tile(target_x, target_y)
                if (
                    tile is None
                    or tile.critter is not critter
                    or not self.is_habitable_tile(tile)
                ):
                    continue

                if (
                    scavenge_range is not None
                    and self.get_tile_distance(
                        game.world,
                        self.x,
                        self.y,
                        target_x,
                        target_y,
                    )
                    > scavenge_range
                ):
                    continue
                target_positions.add((target_x, target_y))

        if not target_positions:
            return False

        path = self.find_path_to_nearest_position(
            game.world,
            target_positions,
            allow_occupied_target=True,
            max_search_distance=scavenge_range,
        )
        if not path:
            return False

        target_x, target_y = path[0]
        target_tile = game.world.get_tile(target_x, target_y)
        if target_tile is None:
            return False

        if (
            target_tile.critter is not None
            and target_tile.critter.current_behavior == "dying"
            and self.is_valid_scavenge_prey(target_tile.critter, prey_types)
        ):
            prey = target_tile.critter
            self.remove_other_critter(game, prey, self.get_scavenge_predator_name())
            self.move_to(game.world, target_x, target_y, game)
            self.handle_successful_meal(game, self.get_reproduction_meal_value(prey))
            return True

        self.set_behavior("scavenge")
        self.move_to(game.world, target_x, target_y, game)
        return True

    def remove_other_critter(self, game, critter, predator_name=None):
        from entity_cleanup import remove_critter

        if predator_name is None:
            predator_name = type(self).__name__

        remove_critter(game, critter, f"it was eaten by {predator_name} {self.id}")

    def should_feed_on_hunt_target(self, prey):
        return True

    def resolve_hunt_attack(self, game, prey, predator_name=None):
        if self.COMBAT_CAPABLE and prey.COMBAT_CAPABLE:
            return self.resolve_combat_attack(game, prey, predator_name)

        return self.resolve_noncombat_hunt_attack(game, prey, predator_name)

    def resolve_noncombat_hunt_attack(self, game, prey, predator_name=None):
        if self.should_feed_on_hunt_target(prey):
            self.remove_other_critter(game, prey, predator_name)
        else:
            from entity_cleanup import remove_critter

            if predator_name is None:
                predator_name = type(self).__name__
            remove_critter(
                game,
                prey,
                f"it was killed by {predator_name} {self.id}",
            )
        return True

    def get_combat_hit_chance(self, defender):
        combined_power = self.COMBAT_POWER + defender.COMBAT_POWER
        if combined_power <= 0:
            return 0.5
        return min(0.8, max(0.2, self.COMBAT_POWER / combined_power))

    def resolve_combat_attack(self, game, prey, predator_name=None):
        if prey.current_behavior == "dying" or prey.combat_health <= 0:
            return False

        self.set_behavior("combat")
        prey.set_behavior("combat")

        if random.random() < self.get_combat_hit_chance(prey):
            if prey.take_combat_damage(attacker=self):
                return self.resolve_defeated_combat_target(
                    game,
                    prey,
                    predator_name,
                )
            return False

        if self.take_combat_damage(attacker=prey):
            self.start_dying(game)
        return False

    def get_active_retaliation_target(self, game):
        target = getattr(self, "retaliation_target", None)
        if (
            target is None
            or target not in game.critters
            or target.current_behavior == "dying"
        ):
            self.retaliation_target = None
            return None

        tile = game.world.get_tile(target.x, target.y)
        if tile is None or tile.critter is not target:
            self.retaliation_target = None
            return None

        return target

    def try_handle_retaliation(self, game):
        target = self.get_active_retaliation_target(game)
        if target is None:
            return False

        path = self.find_path_to_nearest_tile(
            game.world,
            lambda tile: tile.critter is target and self.is_habitable_tile(tile),
            allow_occupied_target=True,
            max_search_distance=self.get_hunt_range(),
        )
        if not path:
            self.retaliation_target = None
            return False

        target_x, target_y = path[0]
        target_tile = game.world.get_tile(target_x, target_y)
        if target_tile is None or target_tile.critter is not target:
            self.retaliation_target = None
            return False

        self.set_behavior("retaliate")
        should_feed = self.should_feed_on_hunt_target(target)
        if self.resolve_hunt_attack(game, target, self.get_predator_name()):
            self.move_to(game.world, target_x, target_y, game)
            if should_feed:
                self.handle_successful_meal(
                    game,
                    self.get_reproduction_meal_value(target),
                )
        return True

    def resolve_defeated_combat_target(
        self,
        game,
        prey,
        predator_name=None,
    ):
        if self.is_hungry and self.should_feed_on_hunt_target(prey):
            return self.resolve_noncombat_hunt_attack(
                game,
                prey,
                predator_name,
            )

        prey.start_dying(game)
        self.set_behavior("combat_victory")
        return False

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
            return True

        if not self.should_remove_on_failed_displacement(critter):
            return False

        self.remove_other_critter(game, critter)
        meal_points = self.get_displacement_meal_value(critter)
        if meal_points is not None:
            self.handle_successful_meal(game, meal_points)
        return True

    def can_enter_tile(self, tile):
        if tile is None:
            return False

        if tile.critter is not None and not self.can_displace_critter(tile.critter):
            return False

        # Shallows are universal transit terrain.  Species that cannot live
        # there are forced to seek valid habitat on their next update.
        # Evolved offspring may also cross other incompatible terrain while
        # seeking their first valid habitat.
        return (
            self.needs_habitat_relocation
            or tile.terrain == "shallows"
            or self.is_habitable_tile(tile)
        )

    def can_path_through_tile(self, tile):
        return self.can_enter_tile(tile)

    def move_to(self, world, nx, ny, game=None):
        tile = world.get_tile(nx, ny)
        if not self.can_enter_tile(tile):
            return False

        old_tile = world.get_tile(self.x, self.y)

        if tile.critter is not None:
            if game is None:
                return False

            displaced_critter = tile.critter
            if not self.displace_critter(game, world, displaced_critter):
                return False

            if old_tile is not None:
                old_tile.critter = None

        elif old_tile is not None:
            old_tile.critter = None

        self.x = nx
        self.y = ny
        tile.critter = self
        if tile.building is not None and hasattr(tile.building, "trap_critter"):
            tile.building.trap_critter(self)
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

    def try_relocate_to_habitat(self, game):
        path = self.find_path_to_nearest_tile(
            game.world,
            self.is_habitable_tile,
        )
        if not path:
            self.set_behavior("seek_habitat")
            return False

        self.set_behavior("seek_habitat")
        next_x, next_y = path[0]
        return self.move_to(game.world, next_x, next_y, game)

    def try_spawn_adjacent_offspring(self, world, tile_predicate=None):
        directions = CARDINAL_DIRECTIONS[:]
        random.shuffle(directions)

        for dx, dy in directions:
            nx = (self.x + dx) % world.cols
            ny = self.y + dy

            tile = world.get_tile(nx, ny)
            if tile is None or tile.critter is not None:
                continue

            if tile_predicate is not None and not tile_predicate(tile):
                continue

            offspring = self.create_offspring(nx, ny)
            tile.critter = offspring
            self.set_behavior("reproduce")
            return offspring

        return None

    def try_reproduce(self, world):
        if self.REQUIRES_LIQUID_TO_REPRODUCE and not self.is_near_liquid(world):
            self.set_behavior("seek_liquid")
            return None

        if self.is_reproduction_blocked(world):
            self.handle_blocked_reproduction()
            return None

        return self.try_spawn_adjacent_offspring(world, self.can_enter_tile)

    def create_offspring(self, x, y):
        return type(self)(x, y)

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
        return self.find_nearby_critters(world, self.get_flee_predator_types(), self.get_flee_detection_radius())

    def find_nearby_critters(self, world, critter_types, radius):
        if not critter_types or radius <= 0:
            return []

        if not isinstance(critter_types, tuple):
            critter_types = (critter_types,)

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

                if isinstance(tile.critter, critter_types):
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

    def find_path_to_nearest_tile(
        self,
        world,
        target_predicate,
        allow_occupied_target=False,
        max_search_distance=None,
        path_tile_predicate=None,
    ):
        start_pos = (self.x, self.y)
        start_tile = world.get_tile(self.x, self.y)
        if start_tile is not None and target_predicate(start_tile):
            return []

        if path_tile_predicate is None:
            path_tile_predicate = self.can_path_through_tile

        # A range-limited search is substantially cheaper than calling
        # get_tile_distance for every visited tile.  BFS visits positions in
        # increasing path distance, so the first position at the limit need
        # not be expanded.
        queue = deque([(self.x, self.y, 0)])
        came_from = {start_pos: None}

        while queue:
            x, y, distance = queue.popleft()
            if max_search_distance is not None and distance >= max_search_distance:
                continue

            for dx, dy in CARDINAL_DIRECTIONS:
                nx = (x + dx) % world.cols
                ny = y + dy
                if ny < 0 or ny >= world.rows:
                    continue

                next_pos = (nx, ny)
                if next_pos in came_from:
                    continue

                tile = world.get_tile(nx, ny)
                if tile is None:
                    continue

                if target_predicate(tile) and (allow_occupied_target or path_tile_predicate(tile)):
                    came_from[next_pos] = (x, y)
                    return self.reconstruct_path(came_from, next_pos)

                if path_tile_predicate(tile):
                    came_from[next_pos] = (x, y)
                    queue.append((nx, ny, distance + 1))

        return None

    def find_path_to_nearest_position(
        self,
        world,
        target_positions,
        allow_occupied_target=False,
        max_search_distance=None,
        path_tile_predicate=None,
    ):
        targets = set(target_positions)
        if not targets:
            return None

        start_pos = (self.x, self.y)
        if start_pos in targets:
            return []

        if path_tile_predicate is None:
            path_tile_predicate = self.can_path_through_tile

        def distance_to_nearest_target(x, y):
            return min(
                self.get_tile_distance(world, x, y, tx, ty)
                for tx, ty in targets
            )

        frontier = []
        heappush(
            frontier,
            (distance_to_nearest_target(*start_pos), 0, *start_pos),
        )
        came_from = {start_pos: None}
        best_distance = {start_pos: 0}

        while frontier:
            _, distance, x, y = heappop(frontier)
            position = (x, y)
            if distance != best_distance.get(position):
                continue
            if position in targets:
                return self.reconstruct_path(came_from, position)
            if max_search_distance is not None and distance >= max_search_distance:
                continue

            for dx, dy in CARDINAL_DIRECTIONS:
                nx = (x + dx) % world.cols
                ny = y + dy
                if ny < 0 or ny >= world.rows:
                    continue

                next_pos = (nx, ny)
                next_distance = distance + 1
                if next_distance >= best_distance.get(next_pos, float("inf")):
                    continue

                tile = world.get_tile(nx, ny)
                if tile is None:
                    continue

                is_target = next_pos in targets
                if not (
                    path_tile_predicate(tile)
                    or (is_target and allow_occupied_target)
                ):
                    continue

                came_from[next_pos] = position
                best_distance[next_pos] = next_distance
                priority = (
                    next_distance
                    + distance_to_nearest_target(nx, ny)
                )
                heappush(frontier, (priority, next_distance, nx, ny))

        return None

    def forage_nearest_tile(
        self,
        game,
        current_tile_predicate,
        path_target_predicate,
        seek_behavior,
        on_feed,
    ):
        current_tile = game.world.get_tile(self.x, self.y)
        if current_tile is not None and current_tile_predicate(current_tile):
            on_feed(current_tile)
            return True

        path = self.find_path_to_nearest_tile(
            game.world,
            path_target_predicate,
            max_search_distance=self.get_forage_range(),
        )
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

    def feed_on_nearest_terrain(self, game, feed_terrains, seek_behavior, on_feed=None, require_empty_target=False):
        if on_feed is None:
            on_feed = lambda tile: self.handle_successful_meal(game)

        self.forage_nearest_tile(
            game,
            lambda tile: tile.terrain in feed_terrains,
            lambda tile: tile.terrain in feed_terrains and (not require_empty_target or tile.critter is None),
            seek_behavior,
            on_feed,
        )

    def hunt_nearest_prey(self, game, prey_types, predator_name=None):
        if not isinstance(prey_types, tuple):
            prey_types = (prey_types,)

        if not self.has_indexed_hunt_candidate(game, prey_types):
            self.set_behavior("hungry")
            return False

        path = self.find_path_to_nearest_tile(
            game.world,
            lambda tile: (
                tile.critter is not None
                and tile.critter is not self
                and self.is_valid_hunt_prey(tile.critter, prey_types)
                and self.is_habitable_tile(tile)
            ),
            allow_occupied_target=True,
            max_search_distance=self.get_hunt_range(),
        )
        if not path:
            self.set_behavior("hungry")
            return False

        target_x, target_y = path[0]
        target_tile = game.world.get_tile(target_x, target_y)
        if target_tile is None:
            self.set_behavior("hungry")
            return False

        if (
            target_tile.critter is not None
            and self.is_valid_hunt_prey(target_tile.critter, prey_types)
        ):
            prey = target_tile.critter
            prey_value = self.get_reproduction_meal_value(prey)
            should_feed = self.should_feed_on_hunt_target(prey)
            if self.resolve_hunt_attack(game, prey, predator_name):
                self.move_to(game.world, target_x, target_y, game)
                if should_feed:
                    self.handle_successful_meal(game, prey_value)
            return True

        self.set_behavior("hunt")
        self.move_to(game.world, target_x, target_y, game)
        return True

    def has_indexed_hunt_candidate(self, game, prey_types):
        critter_type_index = getattr(game, "critter_type_index", None)
        if critter_type_index is None:
            return True

        hunt_range = self.get_hunt_range()
        for critter_type, candidates in critter_type_index.items():
            if not issubclass(critter_type, prey_types):
                continue

            for candidate in candidates:
                if (
                    candidate is self
                    or not self.is_valid_hunt_prey(candidate, prey_types)
                ):
                    continue

                for target_x, target_y in candidate.get_occupied_positions():
                    tile = game.world.get_tile(target_x, target_y)
                    if (
                        tile is None
                        or tile.critter is not candidate
                        or not self.is_habitable_tile(tile)
                    ):
                        continue

                    if (
                        hunt_range is None
                        or self.get_tile_distance(
                            game.world,
                            self.x,
                            self.y,
                            target_x,
                            target_y,
                        )
                        <= hunt_range
                    ):
                        return True

        return False

    def take_hungry_action(self, game):
        prey_types = self.get_hunt_prey_types()
        if prey_types:
            self.hunt_nearest_prey(game, prey_types, self.get_predator_name())
            return

        self.set_behavior("hungry")
