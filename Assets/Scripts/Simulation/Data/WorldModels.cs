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
        public SimEventScope Scope = SimEventScope.Regional;
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
        /// Independent production capacity per resource. Must NOT be derived from current stock.
        /// </summary>
        public float[] ProductionCapacity = new float[7];

        public float BaseWaterStorageCapacity = 10000f;
        public float LandCarryingCapacity = 50000f;
        public bool IsSeaRegion;

        /// <summary>Stability is not a 0..1 percent; values above 1 are valid.</summary>
        public float Stability = 1f;
        public float Education = 0.3f;
        public float FaithLevel = 0.3f;
        public float DiseasePressure;
        public float WeatherFactor = 1f;
        public string LastEvent = "平静";
        public float PopulationDelta;
        public List<RegionEvent> ActiveEvents = new List<RegionEvent>();

        public RegionObserverInfluence Influence = new RegionObserverInfluence();

        public float LastCarryingCapacity;
        public float LastWaterCapacity;
        public float LastFoodSpoilage;
        public float LastFoodProduction;
        public float LastWaterFactor = 1f;
        public float LastFoodReserveDays;
        public float LastNaturalDeath;
        public float LastDiseaseDeath;
        public float LastAgriculturalWaterUsed;
        public float LastLivingWaterUsed;

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

        public bool HasActiveEvent(SimEventType type, int totalDay)
        {
            if (ActiveEvents == null)
            {
                return false;
            }

            for (int i = 0; i < ActiveEvents.Count; i++)
            {
                if (ActiveEvents[i].EventType == type && ActiveEvents[i].IsActiveOn(totalDay))
                {
                    return true;
                }
            }

            return false;
        }

        public float GetActiveEventSeverity(SimEventType type, int totalDay)
        {
            float severity = 0f;
            if (ActiveEvents == null)
            {
                return 0f;
            }

            for (int i = 0; i < ActiveEvents.Count; i++)
            {
                var e = ActiveEvents[i];
                if (e.EventType == type && e.IsActiveOn(totalDay))
                {
                    severity = Mathf.Max(severity, e.Severity);
                }
            }

            return severity;
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
                LastWaterFactor = LastWaterFactor,
                LastFoodReserveDays = LastFoodReserveDays,
                LastNaturalDeath = LastNaturalDeath,
                LastDiseaseDeath = LastDiseaseDeath,
                LastAgriculturalWaterUsed = LastAgriculturalWaterUsed,
                LastLivingWaterUsed = LastLivingWaterUsed,
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
                        Scope = e.Scope,
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
        public string WorldName = "初始大陆与近海";
        public int Year = 1;
        public int DayOfYear = 1;
        public int TotalDays;
        public SeasonId CurrentSeason = SeasonId.Spring;
        public int SeasonIndex;
        public RegionState[] Regions = Array.Empty<RegionState>();
        public float GlobalChaos;
        public int RandomSeed = 20260810;
        public bool HaltedOnNumericError;
        public string LastNumericError;

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
            int day = dayOfYear < 1 ? 1 : dayOfYear;
            int normalized = ((day - 1) % SimulationConfig.DaysPerYear) + 1;
            if (normalized <= 90) return SeasonId.Spring;
            if (normalized <= 180) return SeasonId.Summer;
            if (normalized <= 270) return SeasonId.Autumn;
            return SeasonId.Winter;
        }

        public void SyncSeasonFromDay()
        {
            CurrentSeason = SeasonFromDayOfYear(DayOfYear);
            SeasonIndex = (int)CurrentSeason;
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
                SeasonIndex = SeasonIndex,
                GlobalChaos = GlobalChaos,
                RandomSeed = RandomSeed,
                HaltedOnNumericError = HaltedOnNumericError,
                LastNumericError = LastNumericError,
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
