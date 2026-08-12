using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Data-driven resource tick: production capacity is independent of current stock.
    /// </summary>
    public static class ResourceSystem
    {
        public static void TickDay(
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
            float labor = Mathf.Clamp(region.Population / config.LaborDivisor, config.LaborMin, config.LaborMax);
            float tech = config.TechBase + region.Education * config.TechFromEducation;
            float env = SanitizePositive(region.WeatherFactor, 1f);
            float harvest = SanitizePositive(influence.HarvestBlessing, 1f);

            float foodProduction = CalculateFoodProduction(region, race, season, config, labor, tech, env, harvest);
            float waterProduction = CalculateWaterProduction(region, season, config, env);
            float timberProduction = CalculateTimberProduction(region, race, config, labor, tech);
            float oreProduction = CalculateOreProduction(region, race, config, labor, tech);
            float faithProduction = region.Population * config.FaithYieldPerCapita * region.FaithLevel * race.FaithTendency;
            float knowledgeProduction = region.Population * config.KnowledgeYieldPerCapita * region.Education * race.KnowledgeTendency;
            float magicProduction = region.Population * config.MagicYieldPerCapita * race.MagicAffinity
                                    * (race.PrefersSea ? config.SeaMagicAffinityBonus : config.LandMagicAffinityBonus);

            float foodConsumption = region.Population * config.FoodNeedPerCapita;
            float waterConsumption = region.Population * config.WaterNeedPerCapita;
            float foodSpoilageRate = config.FoodBaseSpoilageRate * config.FoodSpoilageModifier(season);

            float waterCapacity = GetWaterCapacity(region, season, config);
            region.LastWaterCapacity = waterCapacity;

            region.LastFoodProduction = foodProduction;
            ApplyResource(region, ResourceId.Food, foodProduction, foodConsumption, foodSpoilageRate, float.MaxValue, out float foodSpoil);
            region.LastFoodSpoilage = foodSpoil;

            ApplyResource(region, ResourceId.Water, waterProduction, waterConsumption, 0f, waterCapacity, out _);
            ApplyResource(region, ResourceId.Timber, timberProduction, 0f, 0f, float.MaxValue, out _);
            ApplyResource(region, ResourceId.Ore, oreProduction, 0f, 0f, float.MaxValue, out _);
            ApplyResource(region, ResourceId.Faith, faithProduction, 0f, 0f, float.MaxValue, out _);
            ApplyResource(region, ResourceId.Knowledge, knowledgeProduction, 0f, 0f, float.MaxValue, out _);
            ApplyResource(region, ResourceId.Magic, magicProduction, 0f, 0f, float.MaxValue, out _);

            ApplyFoodPressureSideEffects(region, influence, config, rng);
        }

        public static float CalculateFoodProduction(
            RegionState region,
            RaceDefinition race,
            SeasonId season,
            SimulationConfig config,
            float? laborOverride = null,
            float? techOverride = null,
            float? envOverride = null,
            float? harvestOverride = null)
        {
            float labor = laborOverride ?? Mathf.Clamp(region.Population / config.LaborDivisor, config.LaborMin, config.LaborMax);
            float tech = techOverride ?? (config.TechBase + region.Education * config.TechFromEducation);
            float env = envOverride ?? SanitizePositive(region.WeatherFactor, 1f);
            float harvest = harvestOverride ?? SanitizePositive(region.Influence?.HarvestBlessing ?? 1f, 1f);
            return CalculateFoodProduction(region, race, season, config, labor, tech, env, harvest);
        }

        public static float CalculateFoodProduction(
            RegionState region,
            RaceDefinition race,
            SeasonId season,
            SimulationConfig config,
            float labor,
            float tech,
            float env,
            float harvest)
        {
            // CRITICAL: use ProductionCapacity, never current Food stock.
            float capacity = Mathf.Max(0f, region.GetProductionCapacity(ResourceId.Food));
            float value = capacity
                          * labor
                          * tech
                          * env
                          * harvest
                          * race.GrowthFactor
                          * config.FoodProductionModifier(season);
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return 0f;
            }

            return Mathf.Max(0f, value);
        }

        public static float GetWaterCapacity(RegionState region, SeasonId season, SimulationConfig config)
        {
            float baseCap = Mathf.Max(0f, region.BaseWaterStorageCapacity);
            return baseCap * config.WaterCapacityModifier(season);
        }

        static float CalculateWaterProduction(RegionState region, SeasonId season, SimulationConfig config, float env)
        {
            float capacity = Mathf.Max(0f, region.GetProductionCapacity(ResourceId.Water));
            float seasonAvail = season == SeasonId.Winter ? 0.7f : season == SeasonId.Summer ? 0.85f : 1f;
            return Mathf.Max(0f, capacity * 0.02f * env * seasonAvail);
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
            RegionState region,
            ResourceId id,
            float production,
            float consumption,
            float spoilageRate,
            float capacity,
            out float spoilageApplied)
        {
            var type = ResourceCatalog.Get(id);
            float next = ResourceRules.Apply(
                type,
                region.Get(id),
                production,
                consumption,
                spoilageRate,
                capacity,
                out spoilageApplied);
            region.Set(id, next);
        }

        static void ApplyFoodPressureSideEffects(
            RegionState region,
            RegionObserverInfluence influence,
            SimulationConfig config,
            System.Random rng)
        {
            float food = region.Get(ResourceId.Food);
            if (food < region.Population * config.FoodShortageRatio)
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
                region.Stability = Mathf.Min(1.5f, region.Stability + config.FoodSurplusStabilityGain * stabMul);
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
