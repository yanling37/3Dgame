using System;
using System.Collections.Generic;
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
        public float[] Resources = new float[7];
        public float Stability = 1f;
        public float Education = 0.3f;
        public float FaithLevel = 0.3f;
        public float DiseasePressure;
        public float WeatherFactor = 1f;
        public string LastEvent = "平静";
        public float PopulationDelta; // for map trend (approx daily change)
        public List<RegionEvent> ActiveEvents = new List<RegionEvent>();

        public float Get(ResourceId id) => Resources[(int)id];

        public void Set(ResourceId id, float value) => Resources[(int)id] = Mathf.Max(0f, value);

        public void Add(ResourceId id, float delta) => Set(id, Get(id) + delta);

        public RegionState Clone()
        {
            var copy = new RegionState
            {
                Id = Id,
                DisplayName = DisplayName,
                DominantRace = DominantRace,
                Population = Population,
                Resources = (float[])Resources.Clone(),
                Stability = Stability,
                Education = Education,
                FaithLevel = FaithLevel,
                DiseasePressure = DiseasePressure,
                WeatherFactor = WeatherFactor,
                LastEvent = LastEvent,
                PopulationDelta = PopulationDelta,
                ActiveEvents = new List<RegionEvent>()
            };

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
        public int SeasonIndex;
        public float SeasonProgress;
        public RegionState[] Regions = Array.Empty<RegionState>();
        public float GlobalChaos;
        public int RandomSeed;

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
                SeasonProgress = SeasonProgress,
                GlobalChaos = GlobalChaos,
                RandomSeed = RandomSeed,
                Regions = new RegionState[Regions.Length]
            };

            for (int i = 0; i < Regions.Length; i++)
            {
                copy.Regions[i] = Regions[i].Clone();
            }

            return copy;
        }
    }
}
