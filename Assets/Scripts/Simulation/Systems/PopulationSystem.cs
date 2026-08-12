using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Logistic-style population with carrying capacity.
    /// Population may exceed K and may fall to 0; never forced-clamped to K.
    /// </summary>
    public static class PopulationSystem
    {
        public static void TickDay(
            WorldState world,
            RegionState region,
            RaceDefinition race,
            SeasonId season,
            SimulationConfig config)
        {
            if (region == null || race == null || config == null)
            {
                return;
            }

            float previous = region.Population;
            var influence = region.Influence ?? new RegionObserverInfluence();
            float carrying = CalculateCarryingCapacity(region, race, season, config);
            region.LastCarryingCapacity = carrying;

            float foodRatio = 0f;
            if (region.Population > 0f)
            {
                foodRatio = Mathf.Clamp01(region.Get(ResourceId.Food) / Mathf.Max(1f, region.Population * config.FoodRatioSoftCap));
            }

            float fertility = config.BaseFertility
                              * race.FertilityFactor
                              * SanitizePositive(influence.FertilityBlessing, 1f);

            // Logistic dampening: growth slows as Pop approaches K; overshoot allowed (birth→0, deaths continue).
            float logistic = 1f - region.Population / Mathf.Max(1f, carrying);
            float birthLogistic = Mathf.Max(0f, Mathf.Min(logistic, 1.5f));

            float birth = region.Population * fertility * (0.5f + foodRatio) * birthLogistic;
            float naturalDeath = region.Population
                                 * (config.BaseNaturalDeath / Mathf.Max(0.05f, race.LifespanFactor))
                                 * config.DeathModifier(season);
            float diseaseDeath = region.Population
                                 * region.DiseasePressure
                                 * config.DiseaseDeathRate
                                 * config.DiseaseModifier(season)
                                 * SanitizePositive(influence.DiseasePressure, 1f);

            float overpopDeath = 0f;
            if (region.Population > carrying && carrying > 0f)
            {
                overpopDeath = region.Population * config.OverpopulationDeathRate * (region.Population / carrying - 1f);
            }

            region.LastNaturalDeath = naturalDeath + overpopDeath;
            region.LastDiseaseDeath = diseaseDeath;

            float nextPop = region.Population + birth - naturalDeath - diseaseDeath - overpopDeath;
            if (!NumericGuard.AcceptOrHalt(
                    world,
                    region,
                    "Population",
                    previous,
                    nextPop,
                    $"birth={birth}, natDeath={naturalDeath}, diseaseDeath={diseaseDeath}, overpop={overpopDeath}, K={carrying}"))
            {
                // Halt propagation: keep previous finite value (or 0 if previous was already bad).
                region.Population = NumericGuard.IsFinite(previous) && previous > 0f ? previous : 0f;
                region.PopulationDelta = 0f;
                return;
            }

            // Allow extinction; never force a protective floor.
            region.Population = Mathf.Max(0f, nextPop);
            region.PopulationDelta = region.Population - previous;

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
            foodFactor *= Mathf.Clamp01(region.LastWaterFactor <= 0f && region.Get(ResourceId.Water) <= 0f
                ? 0.05f
                : Mathf.Max(0.05f, region.LastWaterFactor > 0f ? region.LastWaterFactor : 1f));

            float waterStock = region.Get(ResourceId.Water);
            float waterNeed = Mathf.Max(1f, Mathf.Max(1f, region.Population) * config.WaterAvailabilityNormPerCapita);
            float waterFactor = Mathf.Clamp(waterStock / waterNeed, 0.05f, 2f);

            float land = Mathf.Max(1f, region.LandCarryingCapacity);
            float tech = config.CarryingTechBase + region.Education * config.CarryingTechFromEducation;

            float weighted =
                land * config.CarryingLandWeight
                + land * foodFactor * config.CarryingFoodWeight
                + land * waterFactor * config.CarryingWaterWeight;

            float carrying = weighted * tech * config.CarryingModifier(season) * Mathf.Max(0.1f, race.GrowthFactor);
            if (!NumericGuard.IsFinite(carrying) || carrying < 1f)
            {
                carrying = 1f;
            }

            return carrying;
        }

        public static float EstimateDailyNetGrowthRate(
            RegionState region,
            RaceDefinition race,
            SeasonId season,
            SimulationConfig config)
        {
            float carrying = Mathf.Max(1f, region.LastCarryingCapacity > 0f
                ? region.LastCarryingCapacity
                : CalculateCarryingCapacity(region, race, season, config));
            float foodRatio = region.Population <= 0f
                ? 0f
                : Mathf.Clamp01(region.Get(ResourceId.Food) / Mathf.Max(1f, region.Population * config.FoodRatioSoftCap));
            float fertility = config.BaseFertility
                              * race.FertilityFactor
                              * SanitizePositive(region.Influence?.FertilityBlessing ?? 1f, 1f);
            float logistic = Mathf.Max(0f, 1f - region.Population / carrying);
            float birthRate = fertility * (0.5f + foodRatio) * logistic;
            float deathRate = (config.BaseNaturalDeath / Mathf.Max(0.05f, race.LifespanFactor)) * config.DeathModifier(season)
                              + region.DiseasePressure * config.DiseaseDeathRate * config.DiseaseModifier(season)
                                * SanitizePositive(region.Influence?.DiseasePressure ?? 1f, 1f);
            if (region.Population > carrying)
            {
                deathRate += config.OverpopulationDeathRate * (region.Population / carrying - 1f);
            }

            return birthRate - deathRate;
        }

        public static float GetDeathModifier(SeasonId season, SimulationConfig config) => config.DeathModifier(season);
        public static float GetDiseaseModifier(SeasonId season, SimulationConfig config) => config.DiseaseModifier(season);

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
