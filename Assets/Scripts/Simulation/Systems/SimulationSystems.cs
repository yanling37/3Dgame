using System.Collections.Generic;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    public static class ResourceSystem
    {
        public static void TickDay(RegionState region, RaceDefinition race, ObserverInfluence influence, System.Random rng)
        {
            float labor = Mathf.Clamp(region.Population / 10000f, 0.2f, 8f);
            float tech = 0.6f + region.Education * 0.8f;
            float env = region.WeatherFactor;
            float harvest = influence.RegionMultiplier(region.Id, influence.HarvestBlessing);

            float foodYield = Base(region, ResourceId.Food) * 0.02f * tech * labor * env * harvest * race.GrowthFactor;
            float waterYield = Base(region, ResourceId.Water) * 0.015f * env;
            float timberYield = race.PrefersSea ? 0.2f : Base(region, ResourceId.Timber) * 0.01f * labor * tech;
            float oreYield = race.PrefersSea ? 0.3f : Base(region, ResourceId.Ore) * 0.008f * labor * tech;
            float faithYield = region.Population * 0.0004f * region.FaithLevel * race.FaithTendency;
            float knowledgeYield = region.Population * 0.00025f * region.Education * race.KnowledgeTendency;
            float magicYield = region.Population * 0.0001f * race.MagicAffinity * (race.PrefersSea ? 1.4f : 0.7f);

            region.Add(ResourceId.Food, foodYield);
            region.Add(ResourceId.Water, waterYield);
            region.Add(ResourceId.Timber, timberYield);
            region.Add(ResourceId.Ore, oreYield);
            region.Add(ResourceId.Faith, faithYield);
            region.Add(ResourceId.Knowledge, knowledgeYield);
            region.Add(ResourceId.Magic, magicYield);

            // Consumption
            float foodNeed = region.Population * 0.02f;
            float waterNeed = region.Population * 0.015f;
            region.Add(ResourceId.Food, -foodNeed);
            region.Add(ResourceId.Water, -waterNeed);

            if (region.Get(ResourceId.Food) < region.Population * 0.2f)
            {
                region.DiseasePressure = Mathf.Clamp01(region.DiseasePressure + 0.01f * influence.RegionMultiplier(region.Id, influence.DiseaseCurse));
                region.Stability = Mathf.Max(0.05f, region.Stability - 0.004f);
                if (rng.NextDouble() < 0.08)
                {
                    region.LastEvent = "粮食短缺引发不安";
                }
            }
            else if (region.Get(ResourceId.Food) > region.Population * 0.8f)
            {
                region.Stability = Mathf.Min(1.5f, region.Stability + 0.0015f * influence.RegionMultiplier(region.Id, influence.StabilityBlessing));
            }

            // Soft weather drift
            region.WeatherFactor = Mathf.Clamp(region.WeatherFactor + ((float)rng.NextDouble() - 0.5f) * 0.02f, 0.6f, 1.3f);
        }

        static float Base(RegionState region, ResourceId id)
        {
            return Mathf.Max(50f, region.Get(id));
        }
    }

    public static class PopulationSystem
    {
        public static void TickDay(RegionState region, RaceDefinition race, ObserverInfluence influence, System.Random rng)
        {
            float fertility = 0.00035f * race.FertilityFactor * influence.RegionMultiplier(region.Id, influence.FertilityBlessing);
            float foodRatio = Mathf.Clamp01(region.Get(ResourceId.Food) / Mathf.Max(1f, region.Population * 0.5f));
            float birth = region.Population * fertility * (0.5f + foodRatio);
            float naturalDeath = region.Population * (0.00022f / race.LifespanFactor);
            float diseaseDeath = region.Population * region.DiseasePressure * 0.0015f * influence.RegionMultiplier(region.Id, influence.DiseaseCurse);

            region.Population = Mathf.Max(100f, region.Population + birth - naturalDeath - diseaseDeath);
            region.DiseasePressure = Mathf.Clamp01(region.DiseasePressure * 0.995f);

            // Education / faith slow drift from resources
            float knowledge = region.Get(ResourceId.Knowledge);
            float faith = region.Get(ResourceId.Faith);
            region.Education = Mathf.Clamp01(Mathf.Lerp(region.Education, Mathf.Clamp01(knowledge / 20000f), 0.002f));
            region.FaithLevel = Mathf.Clamp01(Mathf.Lerp(region.FaithLevel, Mathf.Clamp01(faith / 25000f), 0.002f));

            if (rng.NextDouble() < 0.01 && region.Stability < 0.45f)
            {
                region.LastEvent = "地方骚乱传闻";
                region.Stability = Mathf.Max(0.1f, region.Stability - 0.02f);
            }
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
