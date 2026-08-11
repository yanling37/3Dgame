using System.Collections.Generic;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Data-driven daily resource tick: produce → consume → spoil (if perishable).
    /// </summary>
    public static class ResourceSystem
    {
        public static void TickDay(
            RegionState region,
            RaceDefinition race,
            ObserverInfluence influence,
            SeasonId season,
            System.Random rng)
        {
            float labor = Mathf.Clamp(region.Population / 10000f, 0.2f, 8f);
            float tech = 0.6f + region.Education * 0.8f;
            float env = region.WeatherFactor;
            float harvest = influence.RegionMultiplier(region.Id, influence.HarvestBlessing);

            foreach (var def in ResourceCatalog.All)
            {
                float produced = EstimateDailyProduction(region, race, def, season, labor, tech, env, harvest);
                region.Add(def.Id, produced);

                float consumed = def.BaseConsumePerCapita * region.Population;
                if (consumed > 0f)
                {
                    region.Add(def.Id, -consumed);
                }

                if (def.CanSpoil && def.BaseSpoilRate > 0f)
                {
                    float spoil = region.Get(def.Id) * def.BaseSpoilRate;
                    region.Add(def.Id, -spoil);
                }
            }

            float foodNow = region.Get(ResourceId.Food);
            float diseaseSeason = SeasonSystem.DiseaseModifier(season);
            if (foodNow < region.Population * 0.2f)
            {
                region.DiseasePressure = Mathf.Clamp01(
                    region.DiseasePressure
                    + 0.01f * diseaseSeason * influence.RegionMultiplier(region.Id, influence.DiseaseCurse));
                region.Stability = Mathf.Max(0.05f, region.Stability - 0.004f);
            }
            else if (foodNow > region.Population * 0.8f)
            {
                region.Stability = Mathf.Min(
                    1.5f,
                    region.Stability + 0.0015f * influence.RegionMultiplier(region.Id, influence.StabilityBlessing));
            }

            SeasonSystem.TickWeather(region, season, rng);
        }

        /// <summary>
        /// Shared production estimate for Daily and FastForward macro models.
        /// </summary>
        public static float EstimateDailyProduction(
            RegionState region,
            RaceDefinition race,
            ResourceDefinition def,
            SeasonId season,
            float labor,
            float tech,
            float env,
            float harvest)
        {
            float seasonMul = def.GetSeasonProduction(season);

            if (def.Id == ResourceId.Faith)
            {
                return region.Population * def.BaseProductionScale * region.FaithLevel * race.FaithTendency * seasonMul;
            }

            if (def.Id == ResourceId.Knowledge)
            {
                return region.Population * def.BaseProductionScale * region.Education * race.KnowledgeTendency * seasonMul;
            }

            if (def.Id == ResourceId.Magic)
            {
                float seaMul = race.PrefersSea ? 1.4f : 0.7f;
                return region.Population * def.BaseProductionScale * race.MagicAffinity * seaMul * seasonMul;
            }

            if (def.SeaSuppressed && race.PrefersSea)
            {
                return def.SeaFallbackYield * seasonMul;
            }

            // Food/Water must NOT scale with current stock (avoids exponential inventory blow-up).
            // Use labor/population productivity proxies instead.
            if (def.Id == ResourceId.Food)
            {
                // ~ labor*400 at default scales → mid-pop regions roughly cover consumption.
                float farmland = Mathf.Max(80f, labor * 400f);
                return farmland * def.BaseProductionScale * 50f * seasonMul * tech * env * harvest * race.GrowthFactor;
            }

            if (def.Id == ResourceId.Water)
            {
                float aquifer = Mathf.Max(100f, region.Population * 0.02f);
                return aquifer * def.BaseProductionScale * 40f * seasonMul * env;
            }

            // Extractive resources: mild stock coupling with soft cap.
            float stockBase = Mathf.Min(Mathf.Max(50f, region.Get(def.Id)), 20000f);
            float value = stockBase * def.BaseProductionScale * seasonMul;
            if (def.UsesLabor) value *= labor;
            if (def.UsesTech) value *= tech;
            if (def.UsesWeather) value *= env;
            if (def.UsesHarvestBlessing) value *= harvest;
            return value;
        }

        public static float EstimateCarryingCapacity(RegionState region, RaceDefinition race, SeasonId season)
        {
            float labor = Mathf.Clamp(region.Population / 10000f, 0.2f, 8f);
            float tech = 0.6f + region.Education * 0.8f;
            float env = Mathf.Max(0.5f, region.WeatherFactor);
            var foodDef = ResourceCatalog.Get(ResourceId.Food);
            float foodProd = EstimateDailyProduction(region, race, foodDef, season, labor, tech, env, 1f);
            float water = Mathf.Max(1f, region.Get(ResourceId.Water));
            // Food can support pop if daily prod covers ~0.02/person; water soft-caps.
            float fromFood = foodProd / 0.02f;
            float fromWater = water / 0.5f;
            float fromStock = region.Get(ResourceId.Food) / 0.4f;
            return Mathf.Max(500f, Mathf.Min(fromFood + fromStock * 0.25f, fromWater + fromFood));
        }
    }

    public static class PopulationSystem
    {
        public static void TickDay(
            RegionState region,
            RaceDefinition race,
            ObserverInfluence influence,
            SeasonId season,
            System.Random rng)
        {
            float popBefore = region.Population;
            float fertility = 0.00035f
                * race.FertilityFactor
                * SeasonSystem.BirthModifier(season)
                * influence.RegionMultiplier(region.Id, influence.FertilityBlessing);
            float foodRatio = Mathf.Clamp01(
                region.Get(ResourceId.Food) / Mathf.Max(1f, region.Population * 0.5f));
            float birth = region.Population * fertility * (0.5f + foodRatio);

            float naturalDeath = region.Population
                * (0.00022f / race.LifespanFactor)
                * SeasonSystem.DeathModifier(season);

            float diseaseDeath = region.Population
                * region.DiseasePressure
                * 0.0015f
                * SeasonSystem.DiseaseModifier(season)
                * influence.RegionMultiplier(region.Id, influence.DiseaseCurse);

            region.Population = Mathf.Max(100f, region.Population + birth - naturalDeath - diseaseDeath);
            region.PopulationDelta = region.Population - popBefore;
            region.DiseasePressure = Mathf.Clamp01(region.DiseasePressure * 0.995f);

            float knowledge = region.Get(ResourceId.Knowledge);
            float faith = region.Get(ResourceId.Faith);
            region.Education = Mathf.Clamp01(
                Mathf.Lerp(region.Education, Mathf.Clamp01(knowledge / 20000f), 0.002f));
            region.FaithLevel = Mathf.Clamp01(
                Mathf.Lerp(region.FaithLevel, Mathf.Clamp01(faith / 25000f), 0.002f));

            if (rng.NextDouble() < 0.01 && region.Stability < 0.45f)
            {
                region.Stability = Mathf.Max(0.1f, region.Stability - 0.02f);
            }
        }

        public static float EstimateDailyNetGrowthRate(
            RegionState region,
            RaceDefinition race,
            ObserverInfluence influence,
            SeasonId season)
        {
            float fertility = 0.00035f
                * race.FertilityFactor
                * SeasonSystem.BirthModifier(season)
                * influence.RegionMultiplier(region.Id, influence.FertilityBlessing);
            float foodRatio = Mathf.Clamp01(
                region.Get(ResourceId.Food) / Mathf.Max(1f, region.Population * 0.5f));
            float birthRate = fertility * (0.5f + foodRatio);
            float deathRate = (0.00022f / race.LifespanFactor) * SeasonSystem.DeathModifier(season)
                + region.DiseasePressure * 0.0015f * SeasonSystem.DiseaseModifier(season)
                * influence.RegionMultiplier(region.Id, influence.DiseaseCurse);
            return birthRate - deathRate;
        }
    }

    public static class RegionLookup
    {
        public static RaceDefinition FindRace(IReadOnlyList<RaceDefinition> races, RaceId id)
        {
            for (int i = 0; i < races.Count; i++)
            {
                if (races[i].Id == id)
                {
                    return races[i];
                }
            }

            return races[0];
        }
    }
}
