using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Data-driven resource tick. Food production depends on WaterAvailability (living vs agriculture split).
    /// </summary>
    public static class ResourceSystem
    {
        public static void TickDay(
            WorldState world,
            RegionState region,
            RaceDefinition race,
            SeasonId season,
            SimulationConfig config,
            System.Random rng)
        {
            if (region == null || race == null || config == null)
            {
                return;
            }

            var influence = region.Influence ?? new RegionObserverInfluence();
            float labor = ComputeLabor(region, config);
            float tech = config.TechBase + region.Education * config.TechFromEducation;
            float env = SanitizePositive(region.WeatherFactor, 1f);
            float harvest = SanitizePositive(influence.HarvestBlessing, 1f);
            float eventFoodMul = EventSystem.GetFoodProductionEventModifier(region, world?.TotalDays ?? 0, config);

            float unconstrainedFood = CalculateUnconstrainedFoodProduction(
                region, race, season, config, labor, tech, env, harvest) * eventFoodMul;

            ResolveWaterAllocation(
                region,
                unconstrainedFood,
                config,
                out float waterFactor,
                out float livingWaterUsed,
                out float agriWaterUsed,
                out float totalWaterConsumption);

            float foodProduction = unconstrainedFood * waterFactor;
            region.LastWaterFactor = waterFactor;
            region.LastAgriculturalWaterUsed = agriWaterUsed;
            region.LastLivingWaterUsed = livingWaterUsed;
            region.LastFoodProduction = foodProduction;

            float waterProduction = CalculateWaterProduction(region, season, config, env);
            float timberProduction = CalculateTimberProduction(region, race, config, labor, tech);
            float oreProduction = CalculateOreProduction(region, race, config, labor, tech);
            float faithProduction = SoftSaturatedYield(
                region.Population * config.FaithYieldPerCapita * region.FaithLevel * race.FaithTendency,
                region.Get(ResourceId.Faith),
                config.FaithStockSoftCap);
            float knowledgeProduction = SoftSaturatedYield(
                region.Population * config.KnowledgeYieldPerCapita * region.Education * race.KnowledgeTendency,
                region.Get(ResourceId.Knowledge),
                config.KnowledgeStockSoftCap);
            float magicProduction = SoftSaturatedYield(
                region.Population * config.MagicYieldPerCapita * race.MagicAffinity
                * (race.PrefersSea ? config.SeaMagicAffinityBonus : config.LandMagicAffinityBonus),
                region.Get(ResourceId.Magic),
                config.MagicStockSoftCap);

            float foodConsumption = DailyFoodConsumption(region, config);
            float foodSpoilageRate = config.FoodBaseSpoilageRate * config.FoodSpoilageModifier(season);
            float waterCapacity = GetWaterCapacity(region, season, config);
            region.LastWaterCapacity = waterCapacity;

            ApplyResource(world, region, ResourceId.Food, foodProduction, foodConsumption, foodSpoilageRate, float.MaxValue, out float foodSpoil);
            region.LastFoodSpoilage = foodSpoil;
            region.LastFoodReserveDays = GetFoodReserveDays(region, config);

            ApplyResource(world, region, ResourceId.Water, waterProduction, totalWaterConsumption, 0f, waterCapacity, out _);
            ApplyResource(world, region, ResourceId.Timber, timberProduction, 0f, 0f, float.MaxValue, out _);
            ApplyResource(world, region, ResourceId.Ore, oreProduction, 0f, 0f, float.MaxValue, out _);
            ApplyResource(world, region, ResourceId.Faith, faithProduction, 0f, 0f, float.MaxValue, out _);
            ApplyResource(world, region, ResourceId.Knowledge, knowledgeProduction, 0f, 0f, float.MaxValue, out _);
            ApplyResource(world, region, ResourceId.Magic, magicProduction, 0f, 0f, float.MaxValue, out _);

            ApplyFoodPressureSideEffects(region, influence, config, rng);
        }

        public static float DailyFoodConsumption(RegionState region, SimulationConfig config)
        {
            return Mathf.Max(0f, region.Population) * config.FoodNeedPerCapita;
        }

        public static float GetFoodReserveDays(RegionState region, SimulationConfig config)
        {
            float daily = DailyFoodConsumption(region, config);
            if (daily <= 1e-8f)
            {
                return region.Get(ResourceId.Food) > 0f ? float.PositiveInfinity : 0f;
            }

            return region.Get(ResourceId.Food) / daily;
        }

        public static float ComputeLabor(RegionState region, SimulationConfig config)
        {
            if (region.Population <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp(region.Population / config.LaborDivisor, config.LaborMin, config.LaborMax);
        }

        /// <summary>
        /// Food production before WaterFactor (still includes season/weather/harvest/event-ready base).
        /// </summary>
        public static float CalculateUnconstrainedFoodProduction(
            RegionState region,
            RaceDefinition race,
            SeasonId season,
            SimulationConfig config,
            float labor,
            float tech,
            float env,
            float harvest)
        {
            float capacity = Mathf.Max(0f, region.GetProductionCapacity(ResourceId.Food));
            float value = capacity
                          * labor
                          * tech
                          * env
                          * harvest
                          * race.GrowthFactor
                          * config.FoodProductionModifier(season);
            if (!NumericGuard.IsFinite(value))
            {
                return 0f;
            }

            return Mathf.Max(0f, value);
        }

        public static float CalculateFoodProduction(
            RegionState region,
            RaceDefinition race,
            SeasonId season,
            SimulationConfig config,
            int totalDay = 0)
        {
            float labor = ComputeLabor(region, config);
            float tech = config.TechBase + region.Education * config.TechFromEducation;
            float env = SanitizePositive(region.WeatherFactor, 1f);
            float harvest = SanitizePositive(region.Influence?.HarvestBlessing ?? 1f, 1f);
            float unconstrained = CalculateUnconstrainedFoodProduction(region, race, season, config, labor, tech, env, harvest);
            unconstrained *= EventSystem.GetFoodProductionEventModifier(region, totalDay, config);
            ResolveWaterAllocation(region, unconstrained, config, out float waterFactor, out _, out _, out _);
            return unconstrained * waterFactor;
        }

        /// <summary>
        /// Living water has soft priority; agriculture uses the remainder.
        /// WaterFactor is continuous: full when agri water sufficient, near-zero when dry.
        /// </summary>
        public static void ResolveWaterAllocation(
            RegionState region,
            float unconstrainedFoodProduction,
            SimulationConfig config,
            out float waterFactor,
            out float livingWaterUsed,
            out float agriWaterUsed,
            out float totalWaterConsumption)
        {
            float waterStock = Mathf.Max(0f, region.Get(ResourceId.Water));
            float livingNeed = Mathf.Max(0f, region.Population) * config.WaterNeedPerCapita;
            float agriNeed = Mathf.Max(0f, unconstrainedFoodProduction) * config.AgriculturalWaterPerFoodUnit;

            // Soft priority to living demand — do not dump all water into agriculture.
            livingWaterUsed = Mathf.Min(waterStock, livingNeed);
            float remaining = Mathf.Max(0f, waterStock - livingWaterUsed);

            if (agriNeed <= 1e-8f)
            {
                waterFactor = 1f;
                agriWaterUsed = 0f;
            }
            else
            {
                float ratio = remaining / agriNeed;
                waterFactor = ContinuousWaterFactor(ratio, config.AgriculturalWaterFactorSteepness);
                agriWaterUsed = Mathf.Min(remaining, agriNeed * waterFactor);
            }

            totalWaterConsumption = livingWaterUsed + agriWaterUsed;
            region.LastWaterFactor = waterFactor;
        }

        public static float ContinuousWaterFactor(float availabilityRatio, float steepness)
        {
            if (availabilityRatio <= 0f)
            {
                return 0f;
            }

            if (availabilityRatio >= 1f)
            {
                return 1f; // plentiful water does not reduce production
            }

            // Continuous ease: mild shortage ≠ cliff to zero.
            float k = Mathf.Max(0.1f, steepness);
            return Mathf.Clamp01(1f - Mathf.Exp(-k * availabilityRatio));
        }

        public static float GetWaterCapacity(RegionState region, SeasonId season, SimulationConfig config)
        {
            float baseCap = Mathf.Max(0f, region.BaseWaterStorageCapacity);
            return baseCap * config.WaterCapacityModifier(season);
        }

        public static float SoftSaturatedYield(float rawProduction, float currentStock, float softCap)
        {
            rawProduction = Mathf.Max(0f, rawProduction);
            if (softCap <= 1f)
            {
                return rawProduction;
            }

            // Diminishing returns as stock approaches/exceeds soft cap — blocks float.MaxValue blow-up.
            return rawProduction / (1f + currentStock / softCap);
        }

        static float CalculateWaterProduction(RegionState region, SeasonId season, SimulationConfig config, float env)
        {
            float capacity = Mathf.Max(0f, region.GetProductionCapacity(ResourceId.Water));
            float seasonAvail = season == SeasonId.Winter ? 0.7f : season == SeasonId.Summer ? 0.85f : 1f;
            return Mathf.Max(0f, capacity * config.WaterProductionRate * env * seasonAvail);
        }

        static float CalculateTimberProduction(RegionState region, RaceDefinition race, SimulationConfig config, float labor, float tech)
        {
            if (race.PrefersSea)
            {
                return config.SeaTimberFlatYield;
            }

            return Mathf.Max(0f, region.GetProductionCapacity(ResourceId.Timber) * config.TimberLaborScale * labor * tech);
        }

        static float CalculateOreProduction(RegionState region, RaceDefinition race, SimulationConfig config, float labor, float tech)
        {
            if (race.PrefersSea)
            {
                return config.SeaOreFlatYield;
            }

            return Mathf.Max(0f, region.GetProductionCapacity(ResourceId.Ore) * config.OreLaborScale * labor * tech);
        }

        static void ApplyResource(
            WorldState world,
            RegionState region,
            ResourceId id,
            float production,
            float consumption,
            float spoilageRate,
            float capacity,
            out float spoilageApplied)
        {
            var type = ResourceCatalog.Get(id);
            float previous = region.Get(id);
            float next = ResourceRules.Apply(
                type,
                previous,
                production,
                consumption,
                spoilageRate,
                capacity,
                out spoilageApplied);

            if (!NumericGuard.AcceptOrHalt(
                    world,
                    region,
                    id + ".Stock",
                    previous,
                    next,
                    $"prod={production}, cons={consumption}, spoil={spoilageApplied}, cap={capacity}"))
            {
                spoilageApplied = 0f;
                return;
            }

            region.Set(id, next);
        }

        static void ApplyFoodPressureSideEffects(
            RegionState region,
            RegionObserverInfluence influence,
            SimulationConfig config,
            System.Random rng)
        {
            float food = region.Get(ResourceId.Food);
            if (food < region.Population * config.FoodShortageRatio || region.LastFoodReserveDays < config.FoodShortageReserveDays)
            {
                float diseaseMul = SanitizePositive(influence.DiseasePressure, 1f);
                region.DiseasePressure = Mathf.Clamp01(region.DiseasePressure + config.FoodShortageDiseaseGain * diseaseMul);
                region.Stability = Mathf.Max(0.05f, region.Stability - config.FoodShortageStabilityLoss);
                if (rng != null && rng.NextDouble() < config.FoodShortageEventChance)
                {
                    region.LastEvent = "粮食短缺引发不安";
                }
            }
            else if (food > region.Population * config.FoodSurplusRatio)
            {
                float stabMul = SanitizePositive(influence.StabilityBlessing, 1f);
                // Stability is allowed above 1; soft ceiling only prevents runaway from this one term.
                region.Stability = Mathf.Min(3f, region.Stability + config.FoodSurplusStabilityGain * stabMul);
            }
        }

        static float SanitizePositive(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return fallback;
            }

            return value;
        }
    }
}
