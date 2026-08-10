using System;
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
        public float[] Resources = new float[7];
        public float Stability = 1f;      // 0..1+
        public float Education = 0.3f;    // 0..1
        public float FaithLevel = 0.3f;   // 0..1
        public float DiseasePressure;     // 0..1
        public float WeatherFactor = 1f;  // yield multiplier
        public string LastEvent = "平静";

        public float Get(ResourceId id) => Resources[(int)id];
        public void Set(ResourceId id, float value) => Resources[(int)id] = Mathf.Max(0f, value);
        public void Add(ResourceId id, float delta) => Set(id, Get(id) + delta);
    }

    [Serializable]
    public class WorldState
    {
        public string WorldName = "初始大陆与近海";
        public int Year = 1;
        public int DayOfYear = 1;
        public int TotalDays;
        public RegionState[] Regions = Array.Empty<RegionState>();
        public float GlobalChaos;
    }
}
