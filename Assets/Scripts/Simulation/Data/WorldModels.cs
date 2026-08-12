using System;
using System.Collections.Generic;
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
    public class RegionEvent
    {
        public string EventId;
        public SimEventType EventType;
        public RegionId RegionId;
        public int StartDay;
        public int Duration;
        public float Severity = 1f;

        public int EndDay => StartDay + Mathf.Max(1, Duration);

        public bool IsActiveOn(int totalDay)
        {
            return totalDay >= StartDay && totalDay < EndDay;
        }
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

        public float Stability = 1f;
        public float Education = 0.3f;
        public float FaithLevel = 0.3f;
        public float DiseasePressure;
        public float WeatherFactor = 1f;
        public string LastEvent = "平静";
        public float PopulationDelta; // for map trend (approx daily change)
        public List<RegionEvent> ActiveEvents = new List<RegionEvent>();

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

        public RegionState Clone()
        {
            var copy = new RegionState
            {
                Id = Id,
                DisplayName = DisplayName,
                DominantRace = DominantRace,
                Population = Population,
                Resources = Resources != null ? (float[])Resources.Clone() : new float[7],
                ProductionCapacity = ProductionCapacity != null ? (float[])ProductionCapacity.Clone() : new float[7],
                BaseWaterStorageCapacity = BaseWaterStorageCapacity,
                LandCarryingCapacity = LandCarryingCapacity,
                IsSeaRegion = IsSeaRegion,
                Stability = Stability,
                Education = Education,
                FaithLevel = FaithLevel,
                DiseasePressure = DiseasePressure,
                WeatherFactor = WeatherFactor,
                LastEvent = LastEvent,
                PopulationDelta = PopulationDelta,
                Influence = Influence != null ? Influence.Clone() : new RegionObserverInfluence(),
                LastCarryingCapacity = LastCarryingCapacity,
                LastWaterCapacity = LastWaterCapacity,
                LastFoodSpoilage = LastFoodSpoilage,
                LastFoodProduction = LastFoodProduction,
                LastNaturalDeath = LastNaturalDeath,
                LastDiseaseDeath = LastDiseaseDeath,
                ActiveEvents = new List<RegionEvent>()
            };

            if (ActiveEvents != null)
            {
                foreach (var e in ActiveEvents)
                {
                    copy.ActiveEvents.Add(new RegionEvent
                    {
                        EventId = e.EventId,
                        EventType = e.EventType,
                        RegionId = e.RegionId,
                        StartDay = e.StartDay,
                        Duration = e.Duration,
                        Severity = e.Severity
                    });
                }
            }

            return copy;
        }
    }

    [Serializable]
    public class WorldState
    {
        public const int DaysPerYear = 360;
        public const int DaysPerSeason = 90;

        public string WorldName = "初始大陆与近海";
        public int Year = 1;
        public int DayOfYear = 1;
        public int TotalDays;
        public SeasonId CurrentSeason = SeasonId.Spring;
        public RegionState[] Regions = Array.Empty<RegionState>();
        public float GlobalChaos;
        public int RandomSeed;

        public int CurrentYear => Year;

        public int SeasonIndex => (int)CurrentSeason;

        public int DayInSeason
        {
            get
            {
                int day = Mathf.Clamp(DayOfYear, 1, DaysPerYear);
                return ((day - 1) % DaysPerSeason) + 1;
            }
        }

        public float SeasonProgress => (DayInSeason - 1) / (float)DaysPerSeason;

        public static SeasonId SeasonFromDayOfYear(int dayOfYear)
        {
            int day = dayOfYear;
            if (day < 1)
            {
                day = 1;
            }

            int normalized = ((day - 1) % DaysPerYear) + 1;
            if (normalized <= 90) return SeasonId.Spring;
            if (normalized <= 180) return SeasonId.Summer;
            if (normalized <= 270) return SeasonId.Autumn;
            return SeasonId.Winter;
        }

        public void SyncSeasonFromDay()
        {
            CurrentSeason = SeasonFromDayOfYear(DayOfYear);
        }

        public WorldState Clone()
        {
            var copy = new WorldState
            {
                WorldName = WorldName,
                Year = Year,
                DayOfYear = DayOfYear,
                TotalDays = TotalDays,
                CurrentSeason = CurrentSeason,
                GlobalChaos = GlobalChaos,
                RandomSeed = RandomSeed,
                Regions = new RegionState[Regions != null ? Regions.Length : 0]
            };

            if (Regions != null)
            {
                for (int i = 0; i < Regions.Length; i++)
                {
                    copy.Regions[i] = Regions[i].Clone();
                }
            }

            return copy;
        }
    }
}
