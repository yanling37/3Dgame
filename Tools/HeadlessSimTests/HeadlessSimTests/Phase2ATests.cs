using System;
using System.Collections.Generic;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using DivineWorld.Simulation.Systems;

namespace HeadlessSimTests
{
    public static class Phase2ATests
    {
        public static int RunAll()
        {
            var failures = new List<string>();
            Run("Test1_YearIncrementsAfter360Days", Test1_YearIncrementsAfter360Days, failures);
            Run("Test2_SeasonCycle", Test2_SeasonCycle, failures);
            Run("Test3_FoodSpoils", Test3_FoodSpoils, failures);
            Run("Test4_SummerSpoilageGreaterThanWinter", Test4_SummerSpoilageGreaterThanWinter, failures);
            Run("Test5_MagicDoesNotAutoDecay", Test5_MagicDoesNotAutoDecay, failures);
            Run("Test6_WaterRespectsCapacity", Test6_WaterRespectsCapacity, failures);
            Run("Test7_SeaWaterCapacityHigher", Test7_SeaWaterCapacityHigher, failures);
            Run("Test8_PopulationNotExplosive", Test8_PopulationNotExplosive, failures);
            Run("Test9_WinterDeathPressureHigher", Test9_WinterDeathPressureHigher, failures);
            Run("Test10_SummerDiseaseModifierHigher", Test10_SummerDiseaseModifierHigher, failures);
            Run("Test11_FoodProductionIndependentOfStock", Test11_FoodProductionIndependentOfStock, failures);
            Run("Test12_RegionInfluencesIndependent", Test12_RegionInfluencesIndependent, failures);
            Run("Validation_FullYearStability", Validation_FullYearStability, failures);

            Console.WriteLine();
            Console.WriteLine($"Result: {13 - failures.Count}/13 passed");
            foreach (var f in failures)
            {
                Console.WriteLine("FAIL: " + f);
            }

            return failures.Count == 0 ? 0 : 1;
        }

        static void Run(string name, Action test, List<string> failures)
        {
            try
            {
                test();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception ex)
            {
                failures.Add(name + " :: " + ex.Message);
                Console.WriteLine($"FAIL {name} :: {ex.Message}");
            }
        }

        static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        static void Test1_YearIncrementsAfter360Days()
        {
            var world = new HeadlessWorld();
            AssertTrue(world.State.Year == 1 && world.State.DayOfYear == 1, "start year/day");
            world.AdvanceDays(360);
            AssertTrue(world.State.Year == 2, $"expected Year=2 got {world.State.Year}");
            AssertTrue(world.State.DayOfYear == 1, $"expected DayOfYear=1 got {world.State.DayOfYear}");
            AssertTrue(world.State.CurrentSeason == SeasonId.Spring, "season should be Spring after rollover");
        }

        static void Test2_SeasonCycle()
        {
            var world = new HeadlessWorld();
            var seen = new List<SeasonId>();

            void Capture()
            {
                if (seen.Count == 0 || seen[seen.Count - 1] != world.State.CurrentSeason)
                {
                    seen.Add(world.State.CurrentSeason);
                }
            }

            Capture();
            for (int d = 0; d < 360; d++)
            {
                world.AdvanceDay();
                Capture();
            }

            AssertTrue(seen.Count >= 5, $"expected full cycle entries, got [{string.Join(",", seen)}]");
            AssertTrue(seen[0] == SeasonId.Spring, "start Spring");
            AssertTrue(seen[1] == SeasonId.Summer, "then Summer");
            AssertTrue(seen[2] == SeasonId.Autumn, "then Autumn");
            AssertTrue(seen[3] == SeasonId.Winter, "then Winter");
            AssertTrue(seen[4] == SeasonId.Spring, "then Spring again");
        }

        static void Test3_FoodSpoils()
        {
            var world = new HeadlessWorld();
            var region = world.Region(RegionId.Empire);
            region.Population = 1000f;
            region.Set(ResourceId.Food, 50000f);
            region.SetProductionCapacity(ResourceId.Food, 0f);
            // Freeze weather/influence noise impact by zeroing needs via tiny pop already set.
            float before = region.Get(ResourceId.Food);
            // Tick one summer day with no production.
            world.State.DayOfYear = 100; // Summer
            world.State.SyncSeasonFromDay();
            float rate = world.Config.FoodBaseSpoilageRate * world.Config.FoodSpoilageModifier(SeasonId.Summer);
            ResourceSystem.TickDay(region, world.Races[0], SeasonId.Summer, world.Config, world.Rng);
            float after = region.Get(ResourceId.Food);
            AssertTrue(region.LastFoodSpoilage > 0f, "spoilage diagnostic should be > 0");
            AssertTrue(after < before, $"food should decrease by spoilage/consumption; before={before} after={after} rate={rate}");
        }

        static void Test4_SummerSpoilageGreaterThanWinter()
        {
            var cfg = SimulationConfig.CreateDefault();
            float summer = cfg.FoodBaseSpoilageRate * cfg.FoodSpoilageModifier(SeasonId.Summer);
            float spring = cfg.FoodBaseSpoilageRate * cfg.FoodSpoilageModifier(SeasonId.Spring);
            float autumn = cfg.FoodBaseSpoilageRate * cfg.FoodSpoilageModifier(SeasonId.Autumn);
            float winter = cfg.FoodBaseSpoilageRate * cfg.FoodSpoilageModifier(SeasonId.Winter);
            AssertTrue(summer >= spring, $"Summer({summer}) >= Spring({spring})");
            AssertTrue(spring > autumn, $"Spring({spring}) > Autumn({autumn})");
            AssertTrue(autumn > winter, $"Autumn({autumn}) > Winter({winter})");

            // Empirical: same stock, measure spoilage amount.
            float Spoil(SeasonId season)
            {
                var region = new RegionState
                {
                    Population = 1000f,
                    Resources = new float[7],
                    ProductionCapacity = new float[7],
                    Influence = new RegionObserverInfluence()
                };
                region.Set(ResourceId.Food, 40000f);
                var race = DefaultWorldFactory.CreateRaces()[0];
                ResourceSystem.TickDay(region, race, season, cfg, new Random(1));
                return region.LastFoodSpoilage;
            }

            float s = Spoil(SeasonId.Summer);
            float w = Spoil(SeasonId.Winter);
            AssertTrue(s > w, $"Summer spoilage {s} should be > Winter {w}");
        }

        static void Test5_MagicDoesNotAutoDecay()
        {
            var world = new HeadlessWorld();
            var region = world.Region(RegionId.Theocracy);
            // Isolate: set magic yield path to zero by zero pop contribution workaround —
            // instead compare against catalog rule: no spoilage, and freeze stock with zero production.
            region.Population = 0f; // yields use population; min pop enforced later in pop system only
            // Direct rule check:
            var type = ResourceCatalog.Get(ResourceId.Magic);
            AssertTrue(type.Lifecycle == ResourceLifecycle.Persistent, "Magic must be Persistent");
            AssertTrue(!type.CanSpoil, "Magic must not spoil");

            float stock = 1000f;
            float next = ResourceRules.Apply(type, stock, 0f, 0f, 0.5f, float.MaxValue, out float spoil);
            AssertTrue(spoil == 0f, "persistent resources ignore spoilage rate");
            AssertTrue(Math.Abs(next - stock) < 1e-4f, "magic stock unchanged with zero production/consumption");

            // Multi-day with production disabled via zero population on a disposable region.
            var isolated = new RegionState
            {
                Id = RegionId.Empire,
                Population = 0f,
                Resources = new float[7],
                ProductionCapacity = new float[7],
                Influence = new RegionObserverInfluence(),
                Education = 0f,
                FaithLevel = 0f
            };
            isolated.Set(ResourceId.Magic, 2500f);
            float magicBefore = isolated.Get(ResourceId.Magic);
            for (int i = 0; i < 30; i++)
            {
                ResourceSystem.TickDay(isolated, world.Races[0], SeasonId.Spring, world.Config, world.Rng);
            }

            AssertTrue(Math.Abs(isolated.Get(ResourceId.Magic) - magicBefore) < 1e-3f,
                $"magic changed without production/consumption: {magicBefore} -> {isolated.Get(ResourceId.Magic)}");
        }

        static void Test6_WaterRespectsCapacity()
        {
            var world = new HeadlessWorld();
            var region = world.Region(RegionId.Empire);
            region.SetProductionCapacity(ResourceId.Water, 100000f);
            region.Set(ResourceId.Water, region.BaseWaterStorageCapacity);
            for (int i = 0; i < 20; i++)
            {
                ResourceSystem.TickDay(region, world.Races[0], SeasonId.Spring, world.Config, world.Rng);
                float cap = ResourceSystem.GetWaterCapacity(region, SeasonId.Spring, world.Config);
                AssertTrue(region.Get(ResourceId.Water) <= cap + 1e-3f,
                    $"water {region.Get(ResourceId.Water)} exceeded capacity {cap}");
            }
        }

        static void Test7_SeaWaterCapacityHigher()
        {
            var world = new HeadlessWorld();
            var land = world.Region(RegionId.Empire);
            var sea = world.Region(RegionId.Sea);
            float landCap = ResourceSystem.GetWaterCapacity(land, SeasonId.Spring, world.Config);
            float seaCap = ResourceSystem.GetWaterCapacity(sea, SeasonId.Spring, world.Config);
            AssertTrue(sea.BaseWaterStorageCapacity > land.BaseWaterStorageCapacity, "sea base capacity higher");
            AssertTrue(seaCap > landCap, $"sea cap {seaCap} should be > land {landCap}");
        }

        static void Test8_PopulationNotExplosive()
        {
            var world = new HeadlessWorld();
            // Run 10 years.
            world.AdvanceDays(360 * 10);
            foreach (var region in world.State.Regions)
            {
                float carrying = Math.Max(1f, region.LastCarryingCapacity);
                AssertTrue(region.Population < carrying * 3f,
                    $"{region.DisplayName} exploded: pop={region.Population} carrying~{carrying}");
                AssertTrue(region.Population < 5_000_000f,
                    $"{region.DisplayName} absolute explosion: {region.Population}");
            }
        }

        static void Test9_WinterDeathPressureHigher()
        {
            var cfg = SimulationConfig.CreateDefault();
            float spring = PopulationSystem.GetDeathModifier(SeasonId.Spring, cfg);
            float winter = PopulationSystem.GetDeathModifier(SeasonId.Winter, cfg);
            AssertTrue(winter > spring, $"Winter death mod {winter} should be > Spring {spring}");

            float Death(SeasonId season)
            {
                var region = new RegionState
                {
                    Population = 50000f,
                    Resources = new float[] { 30000, 10000, 0, 0, 0, 0, 0 },
                    ProductionCapacity = new float[] { 1000, 500, 0, 0, 0, 0, 0 },
                    LandCarryingCapacity = 80000f,
                    BaseWaterStorageCapacity = 15000f,
                    DiseasePressure = 0.05f,
                    Education = 0.4f,
                    Influence = new RegionObserverInfluence()
                };
                var race = DefaultWorldFactory.CreateRaces()[0];
                PopulationSystem.TickDay(region, race, season, cfg);
                return region.LastNaturalDeath;
            }

            AssertTrue(Death(SeasonId.Winter) > Death(SeasonId.Spring), "winter natural death amount higher");
        }

        static void Test10_SummerDiseaseModifierHigher()
        {
            var cfg = SimulationConfig.CreateDefault();
            float spring = PopulationSystem.GetDiseaseModifier(SeasonId.Spring, cfg);
            float summer = PopulationSystem.GetDiseaseModifier(SeasonId.Summer, cfg);
            AssertTrue(summer > spring, $"Summer disease {summer} should be > Spring {spring}");

            float DiseaseDeath(SeasonId season)
            {
                var region = new RegionState
                {
                    Population = 50000f,
                    Resources = new float[] { 30000, 10000, 0, 0, 0, 0, 0 },
                    ProductionCapacity = new float[] { 1000, 500, 0, 0, 0, 0, 0 },
                    LandCarryingCapacity = 80000f,
                    BaseWaterStorageCapacity = 15000f,
                    DiseasePressure = 0.4f,
                    Education = 0.4f,
                    Influence = new RegionObserverInfluence()
                };
                var race = DefaultWorldFactory.CreateRaces()[0];
                PopulationSystem.TickDay(region, race, season, cfg);
                return region.LastDiseaseDeath;
            }

            AssertTrue(DiseaseDeath(SeasonId.Summer) > DiseaseDeath(SeasonId.Spring), "summer disease death higher");
        }

        static void Test11_FoodProductionIndependentOfStock()
        {
            var world = new HeadlessWorld();
            var region = world.Region(RegionId.Empire);
            var race = world.Races[0];
            region.WeatherFactor = 1f;
            region.Influence.HarvestBlessing = 1f;
            region.SetProductionCapacity(ResourceId.Food, 1000f);

            region.Set(ResourceId.Food, 100f);
            float lowStockProd = ResourceSystem.CalculateFoodProduction(region, race, SeasonId.Spring, world.Config);

            region.Set(ResourceId.Food, 100000f);
            float highStockProd = ResourceSystem.CalculateFoodProduction(region, race, SeasonId.Spring, world.Config);

            AssertTrue(Math.Abs(lowStockProd - highStockProd) < 1e-4f,
                $"production must not depend on stock: low={lowStockProd} high={highStockProd}");

            // Increasing capacity should increase production.
            region.SetProductionCapacity(ResourceId.Food, 2000f);
            float higherCapProd = ResourceSystem.CalculateFoodProduction(region, race, SeasonId.Spring, world.Config);
            AssertTrue(higherCapProd > highStockProd * 1.5f, "capacity should drive production");
        }

        static void Test12_RegionInfluencesIndependent()
        {
            var world = new HeadlessWorld();
            var a = new RegionObserverInfluence
            {
                FertilityBlessing = 1.3f,
                HarvestBlessing = 1.2f,
                DiseasePressure = 0.8f,
                StabilityBlessing = 1.1f
            };
            var b = new RegionObserverInfluence
            {
                FertilityBlessing = 0.7f,
                HarvestBlessing = 0.8f,
                DiseasePressure = 1.3f,
                StabilityBlessing = 0.9f
            };

            world.Influence.SetRegionInfluence(RegionId.Theocracy, a);
            world.Influence.SetRegionInfluence(RegionId.Empire, b);

            var gotA = world.Influence.GetRegionInfluence(RegionId.Theocracy);
            var gotB = world.Influence.GetRegionInfluence(RegionId.Empire);

            AssertTrue(Math.Abs(gotA.FertilityBlessing - 1.3f) < 1e-5f, "A fertility");
            AssertTrue(Math.Abs(gotB.FertilityBlessing - 0.7f) < 1e-5f, "B fertility");
            AssertTrue(Math.Abs(gotA.DiseasePressure - 0.8f) < 1e-5f, "A disease");
            AssertTrue(Math.Abs(gotB.DiseasePressure - 1.3f) < 1e-5f, "B disease");
            AssertTrue(Math.Abs(gotA.FertilityBlessing - gotB.FertilityBlessing) > 0.1f, "A and B independent");
        }

        static void Validation_FullYearStability()
        {
            var world = new HeadlessWorld();
            var seasons = new HashSet<SeasonId>();
            float weatherMin = float.MaxValue;
            float weatherMax = float.MinValue;
            bool sawFoodSpoil = false;
            bool winterDeathSpike = false;
            bool summerDisease = false;

            float springDeathMod = world.Config.DeathModifier(SeasonId.Spring);
            float winterDeathMod = world.Config.DeathModifier(SeasonId.Winter);
            AssertTrue(winterDeathMod > springDeathMod, "config winter death");

            for (int d = 0; d < 360; d++)
            {
                var seasonBefore = world.State.CurrentSeason;
                seasons.Add(seasonBefore);
                world.AdvanceDay();

                foreach (var region in world.State.Regions)
                {
                    weatherMin = Math.Min(weatherMin, region.WeatherFactor);
                    weatherMax = Math.Max(weatherMax, region.WeatherFactor);
                    if (region.LastFoodSpoilage > 0f) sawFoodSpoil = true;
                    AssertTrue(region.Get(ResourceId.Water) <= region.LastWaterCapacity + 1e-2f, "water clamp during year");
                    AssertTrue(region.Population >= world.Config.MinPopulation, "population floor");
                    AssertTrue(!float.IsNaN(region.Population) && !float.IsInfinity(region.Population), "pop finite");
                    foreach (ResourceId id in Enum.GetValues(typeof(ResourceId)))
                    {
                        float v = region.Get(id);
                        AssertTrue(!float.IsNaN(v) && !float.IsInfinity(v) && v >= 0f, id + " finite non-negative");
                    }
                }

                if (seasonBefore == SeasonId.Winter)
                {
                    winterDeathSpike = winterDeathSpike || world.Config.DeathModifier(seasonBefore) > springDeathMod;
                }

                if (seasonBefore == SeasonId.Summer)
                {
                    summerDisease = summerDisease || world.Config.DiseaseModifier(seasonBefore) > world.Config.DiseaseModifier(SeasonId.Spring);
                }
            }

            AssertTrue(world.State.Year == 2 && world.State.DayOfYear == 1, "full year rollover");
            AssertTrue(seasons.SetEquals(new HashSet<SeasonId>
            {
                SeasonId.Spring, SeasonId.Summer, SeasonId.Autumn, SeasonId.Winter
            }), "all seasons visited");
            AssertTrue(sawFoodSpoil, "food spoiled during the year");
            AssertTrue(winterDeathSpike, "winter death pressure present");
            AssertTrue(summerDisease, "summer disease modifier present");
            AssertTrue(weatherMax - weatherMin > 0.05f, "weather varied with seasons/continuity");

            float seaCap = world.Region(RegionId.Sea).BaseWaterStorageCapacity;
            float landCap = world.Region(RegionId.Empire).BaseWaterStorageCapacity;
            AssertTrue(seaCap > landCap, "sea capacity still higher after year");
        }
    }
}
