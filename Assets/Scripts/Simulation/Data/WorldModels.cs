using System;
using DivineWorld.Simulation.Player;
using UnityEngine;

namespace DivineWorld.Simulation.Data
{
    [Serializable]
    public class RaceDefinition
    {
        public RaceId Id;
        public string DisplayName;
        [Range(0.1f, 5f)] public float LifespanFactor = 1f;
        [Range(0.1f, 5f)] public float FertilityFactor = 1f;
        [Range(0.1f, 5f)] public float GrowthFactor = 1f;
        [Range(0.1f, 5f)] public float StrengthFactor = 1f;
        [Range(0.1f, 5f)] public float WisdomFactor = 1f;
        [Range(0.1f, 5f)] public float MagicAffinity = 1f;
        [Range(0.1f, 3f)] public float AbilityVariance = 1f;
        [Range(0f, 2f)] public float FaithTendency = 1f;
        [Range(0f, 2f)] public float KnowledgeTendency = 1f;
        public bool PrefersSea;
    }

    [Serializable]
    public class RegionState
    {
        public RegionId Id;
        public string DisplayName;
        public RaceId DominantRace;
        public float Population;

        /// <summary>Current resource stocks. Index by ResourceId.</summary>
        public float[] Resources = new float[7];

        /// <summary>
        /// Independent production capacity per resource. Must NOT be derived from current stock
        /// (avoids food→production positive feedback).
        /// </summary>
        public float[] ProductionCapacity = new float[7];

        /// <summary>Base water storage capacity for this region (before seasonal modifier).</summary>
        public float BaseWaterStorageCapacity = 10000f;

        /// <summary>Base land/environment carrying capacity for population model.</summary>
        public float LandCarryingCapacity = 50000f;

        public bool IsSeaRegion;

        public float Stability = 1f;      // 0..1+
        public float Education = 0.3f;    // 0..1
        public float FaithLevel = 0.3f;   // 0..1
        public float DiseasePressure;     // 0..1
        public float WeatherFactor = 1f;  // yield multiplier
        public string LastEvent = "平静";

        /// <summary>Region-specific observer influence (independent per region).</summary>
        public RegionObserverInfluence Influence = new RegionObserverInfluence();

        /// <summary>Last computed carrying capacity (debug / UI).</summary>
        public float LastCarryingCapacity;

        /// <summary>Last water capacity after seasonal modifier (debug / UI).</summary>
        public float LastWaterCapacity;

        /// <summary>Last food spoilage applied (debug / tests).</summary>
        public float LastFoodSpoilage;

        /// <summary>Last food production applied (debug / tests).</summary>
        public float LastFoodProduction;

        /// <summary>Last natural + disease death pressure diagnostics.</summary>
        public float LastNaturalDeath;
        public float LastDiseaseDeath;

        public float Get(ResourceId id) => Resources[(int)id];

        public void Set(ResourceId id, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = 0f;
            }

            Resources[(int)id] = Mathf.Max(0f, value);
        }

        public void Add(ResourceId id, float delta) => Set(id, Get(id) + delta);

        public float GetProductionCapacity(ResourceId id) => ProductionCapacity[(int)id];

        public void SetProductionCapacity(ResourceId id, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                value = 0f;
            }

            ProductionCapacity[(int)id] = Mathf.Max(0f, value);
        }
    }

    [Serializable]
    public class WorldState
    {
        public string WorldName = "初始大陆与近海";
        public int Year = 1;
        public int DayOfYear = 1;
        public int TotalDays;
        public SeasonId CurrentSeason = SeasonId.Spring;
        public RegionState[] Regions = Array.Empty<RegionState>();
        public float GlobalChaos;

        public int CurrentYear => Year;

        public int DayInSeason
        {
            get
            {
                int day = Mathf.Clamp(DayOfYear, 1, SimulationConfig.DaysPerYear);
                return ((day - 1) % SimulationConfig.DaysPerSeason) + 1;
            }
        }

        public float SeasonProgress
        {
            get
            {
                return (DayInSeason - 1) / (float)SimulationConfig.DaysPerSeason;
            }
        }

        public static SeasonId SeasonFromDayOfYear(int dayOfYear)
        {
            int day = dayOfYear;
            if (day < 1)
            {
                day = 1;
            }

            // Normalize into 1..360 for query helpers; year rollover handled by calendar advance.
            int normalized = ((day - 1) % SimulationConfig.DaysPerYear) + 1;
            if (normalized <= 90) return SeasonId.Spring;
            if (normalized <= 180) return SeasonId.Summer;
            if (normalized <= 270) return SeasonId.Autumn;
            return SeasonId.Winter;
        }

        public void SyncSeasonFromDay()
        {
            CurrentSeason = SeasonFromDayOfYear(DayOfYear);
        }
    }
}
