import unittest
from types import SimpleNamespace
from unittest.mock import patch

from critters import ApeSailor, Crab, Fish, Jellyfish, MegaSpider, Nautilus, Newt, Plankton, SeaScorpion, Shark, Snail, SpermWhale, Squid, Therapsid, Trilobite, Wolf
from world import World


class EcologyTests(unittest.TestCase):
    def test_mega_spider_is_combat_capable_and_hunts_other_predators(self):
        spider = MegaSpider(0, 0)
        wolf = Wolf(1, 0)

        self.assertTrue(spider.COMBAT_CAPABLE)
        self.assertTrue(wolf.COMBAT_CAPABLE)
        self.assertTrue(spider.is_valid_hunt_prey(wolf, spider.get_hunt_prey_types()))

    def test_therapsid_splits_hungry_actions_between_grazing_and_hunting(self):
        game = SimpleNamespace()

        critter = Therapsid(0, 0)
        with (
            patch("critters.therapsid.random.random", return_value=0.25),
            patch.object(critter, "feed_on_nearest_terrain", return_value=True) as graze,
            patch.object(critter, "hunt_or_explore") as hunt,
        ):
            critter.take_hungry_action(game)
        graze.assert_called_once()
        hunt.assert_not_called()

        critter = Therapsid(0, 0)
        with (
            patch("critters.therapsid.random.random", return_value=0.75),
            patch.object(critter, "feed_on_nearest_terrain") as graze,
            patch.object(critter, "hunt_or_explore") as hunt,
        ):
            critter.take_hungry_action(game)
        hunt.assert_called_once_with(game)
        graze.assert_not_called()

    def test_therapsid_has_long_food_search_and_starvation_windows(self):
        critter = Therapsid(0, 0)
        self.assertEqual(critter.starvation_interval, 120.0)
        self.assertEqual(critter.get_hunt_range(), 18)
        self.assertEqual(critter.get_forage_range(), 18)

    def test_squid_reproduces_after_five_nutrition(self):
        for predator_type in (Squid, SeaScorpion, Shark):
            with self.subTest(predator_type=predator_type.__name__):
                predator = predator_type(0, 0)
                self.assertEqual(predator.REPRODUCTION_MEAL_THRESHOLD, 5)
                for prey_type in (Fish, Nautilus, Crab):
                    self.assertTrue(
                        predator.matches_prey_selector(
                            prey_type(0, 0), predator.get_hunt_prey_selector()
                        )
                    )
                self.assertFalse(
                    predator.matches_prey_selector(
                        Jellyfish(0, 0), predator.get_hunt_prey_selector()
                    )
                )

    def test_only_sea_scorpions_can_eat_trilobites(self):
        trilobite = Trilobite(0, 0)

        self.assertTrue(trilobite.can_be_eaten_by(SeaScorpion(1, 0)))
        for predator_type in (Shark, Squid, SpermWhale, ApeSailor):
            with self.subTest(predator_type=predator_type.__name__):
                self.assertFalse(trilobite.can_be_eaten_by(predator_type(1, 0)))

    def test_shark_has_very_long_hunger_interval_and_hunts_large_sea_predators(self):
        shark = Shark(0, 0)
        prey = shark.get_hunt_prey_selector()

        self.assertEqual(shark.hunger_interval, 1200.0)
        for prey_type in (Squid, SeaScorpion):
            with self.subTest(prey_type=prey_type.__name__):
                self.assertTrue(shark.matches_prey_selector(prey_type(0, 0), prey))

    def test_sperm_whale_diet_excludes_jellyfish(self):
        whale = SpermWhale(0, 0)
        prey = whale.get_hunt_prey_selector()

        for prey_type in (Squid, SeaScorpion, ApeSailor):
            with self.subTest(prey_type=prey_type.__name__):
                self.assertTrue(whale.matches_prey_selector(prey_type, prey))
        self.assertFalse(whale.matches_prey_selector(Jellyfish, prey))

    def test_small_critters_have_one_nutrition(self):
        for critter_type in (Plankton, Snail, Newt, Crab, Trilobite):
            with self.subTest(critter_type=critter_type.__name__):
                self.assertEqual(critter_type(0, 0).get_food_value(), 1)

    def test_newt_can_reproduce_on_grass_and_then_becomes_old(self):
        world = World(3, 3, default_terrain="grass")
        newt = Newt(1, 1)
        world.get_tile(1, 1).critter = newt
        newt.reproduction_limit = 1
        original_cooldown = newt.move_cooldown

        offspring = newt.try_reproduce(world)

        self.assertIsNotNone(offspring)
        self.assertEqual(newt.reproductions_completed, 1)
        self.assertTrue(newt.is_senescent)
        self.assertEqual(newt.move_cooldown, original_cooldown * 2)
        self.assertIsNone(newt.try_reproduce(world))

    def test_stored_meal_extends_starvation(self):
        world = World(1, 1, default_terrain="grass")
        newt = Newt(0, 0)
        world.get_tile(0, 0).critter = newt
        newt.is_hungry = True
        newt.starvation_timer = 0.1
        newt.meals_eaten = 1
        game = SimpleNamespace(world=world, dying_critters=set())

        self.assertTrue(newt.update_hunger(game, 0.2))
        self.assertEqual(newt.meals_eaten, 0)
        self.assertAlmostEqual(newt.starvation_timer, newt.starvation_interval * 0.5)
        self.assertEqual(newt.current_behavior, "living_on_reserves")

    def test_aquatic_critter_has_fixed_plankton_remains_chance(self):
        world = World(1, 1, default_terrain="ocean")
        fish = Fish(0, 0)
        tile = world.get_tile(0, 0)
        game = SimpleNamespace(world=world, critters=[])

        self.assertTrue(fish.can_leave_plankton_remains(tile))
        with patch("critters.critter.random.random", return_value=0.24):
            self.assertTrue(fish.try_spawn_fixed_aquatic_plankton_remains(game, tile))

        self.assertIsInstance(tile.critter, Plankton)
        self.assertEqual(len(game.critters), 1)


if __name__ == "__main__":
    unittest.main()
