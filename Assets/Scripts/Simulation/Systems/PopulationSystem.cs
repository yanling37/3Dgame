using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Logistic-style population model with carrying capacity and seasonal death/disease modifiers.
    /// </summary>
    public static class PopulationSystem
    {
        public static void TickDay(
            RegionState region,
            RaceDefinition race,
            SeasonId season,
            SimulationConfig config)
        {
            if (region == null || race == null || config == null)
            {
                return;
            }

            var influence = region.Influence ?? new RegionObserverInfluence();
            float popBefore = region.Population;
            float carrying = CalculateCarryingCapacity(region, race, season, config);
            region.LastCarryingCapacity = carrying;

            float foodRatio = Mathf.Clamp01(region.Get(ResourceId.Food) / Mathf.Max(1f, region.Population * config.FoodRatioSoftCap));
            float fertility = config.BaseFertility
                              * race.FertilityFactor
                              * SanitizePositive(influence.FertilityBlessing, 1f);

            // Logistic dampening: growth slows as population approaches carrying capacity.
            float logistic = 1f - region.Population / Mathf.Max(1f, carrying);
            logistic = Mathf.Clamp(logistic, 0f, 1.5f);

            float birth = region.Population * fertility * (0.5f + foodRatio) * Mathf.Max(0f, logistic);
            float naturalDeath = region.Population
                                 * (config.BaseNaturalDeath / Mathf.Max(0.05f, race.LifespanFactor))
                                 * config.DeathModifier(season);
            float diseaseDeath = region.Population
                                 * region.DiseasePressure
                                 * config.DiseaseDeathRate
                                 * config.DiseaseModifier(season)
                                 * SanitizePositive(influence.DiseasePressure, 1f);

            region.LastNaturalDeath = naturalDeath;
            region.LastDiseaseDeath = diseaseDeath;

            float nextPop = region.Population + birth - naturalDeath - diseaseDeath;
            if (float.IsNaN(nextPop) || float.IsInfinity(nextPop))
            {
                nextPop = config.MinPopulation;
            }

            region.Population = Mathf.Max(config.MinPopulation, nextPop);
            region.PopulationDelta = region.Population - popBefore;

            // Disease pressure decay, with summer environmental disease gain.
            region.DiseasePressure = Mathf.Clamp01(region.DiseasePressure * config.DiseaseDecay);
            if (season == SeasonId.Summer)
            {
                float summerGain = 0.003f * config.DiseaseModifier(season) * SanitizePositive(influence.DiseasePressure, 1f);
                region.DiseasePressure = Mathf.Clamp01(region.DiseasePressure + summerGain);
            }
        }

        public static float CalculateCarryingCapacity(
            RegionState region,
            RaceDefinition race,
            SeasonId season,
            SimulationConfig config)
        {
            float foodProdCap = Mathf.Max(0f, region.GetProductionCapacity(ResourceId.Food));
            float foodFactor = foodProdCap / Mathf.Max(1f, config.FoodProductionCapacityNorm);
            foodFactor *= config.FoodProductionModifier(season);

            float waterStock = region.Get(ResourceId.Water);
            float waterNeed = Mathf.Max(1f, region.Population * config.WaterAvailabilityNormPerCapita);
            float waterFactor = Mathf.Clamp(waterStock / waterNeed, 0.1f, 2f);

            float land = Mathf.Max(1f, region.LandCarryingCapacity);
            float tech = config.CarryingTechBase + region.Education * config.CarryingTechFromEducation;

            float weighted =
                land * config.CarryingLandWeight
                + land * foodFactor * config.CarryingFoodWeight
                + land * waterFactor * config.CarryingWaterWeight;

            float carrying = weighted * tech * config.CarryingModifier(season) * Mathf.Max(0.1f, race.GrowthFactor);
            if (float.IsNaN(carrying) || float.IsInfinity(carrying) || carrying < config.MinPopulation)
            {
                carrying = config.MinPopulation;
            }

            return carrying;
        }

        /// <summary>Expose seasonal death multiplier for tests / macro callers.</summary>
        public static float GetDeathModifier(SeasonId season, SimulationConfig config) => config.DeathModifier(season);

        /// <summary>Expose seasonal disease multiplier for tests / macro callers.</summary>
        public static float GetDiseaseModifier(SeasonId season, SimulationConfig config) => config.DiseaseModifier(season);

        /// <summary>Approximate daily net growth rate for macro / fast-forward models.</summary>
        public static float EstimateDailyNetGrowthRate(
            RegionState region,
            RaceDefinition race,
            SeasonId season,
            SimulationConfig config)
        {
            if (region == null || race == null || config == null)
            {
                return 0f;
            }

            var influence = region.Influence ?? new RegionObserverInfluence();
            float foodRatio = Mathf.Clamp01(region.Get(ResourceId.Food) / Mathf.Max(1f, region.Population * config.FoodRatioSoftCap));
            float fertility = config.BaseFertility
                              * race.FertilityFactor
                              * SanitizePositive(influence.FertilityBlessing, 1f);
            float birthRate = fertility * (0.5f + foodRatio);
            float deathRate = (config.BaseNaturalDeath / Mathf.Max(0.05f, race.LifespanFactor)) * config.DeathModifier(season)
                              + region.DiseasePressure
                              * config.DiseaseDeathRate
                              * config.DiseaseModifier(season)
                              * SanitizePositive(influence.DiseasePressure, 1f);
            return birthRate - deathRate;
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
