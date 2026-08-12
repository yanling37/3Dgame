using System;
using System.Collections.Generic;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Immutable world-level observation DTO for Map / HUD / Stats / Graph / Report.
    /// Values are copied from Simulation State — never recomputed by presentation math.
    /// </summary>
    [Serializable]
    public sealed class WorldObservationSnapshot
    {
        public string WorldName;
        public int Year;
        public int DayOfYear;
        public int TotalDays;
        public SeasonId CurrentSeason;
        public int SeasonIndex;
        public int DayInSeason;
        public float SeasonProgress;
        public bool HaltedOnNumericError;
        public string LastNumericError;
        public RegionObservationSnapshot[] Regions = Array.Empty<RegionObservationSnapshot>();

        /// <summary>World totals derived by summing region snapshots (still from State, not formula).</summary>
        public float TotalPopulation;
        public float TotalFood;
        public float TotalWater;
        public float TotalMana;
    }

    /// <summary>Immutable per-region observation DTO.</summary>
    [Serializable]
    public sealed class RegionObservationSnapshot
    {
        public RegionId RegionId;
        public string DisplayName;
        public float Population;
        public float PopulationDelta;
        public float CarryingCapacity;
        public float Food;
        public float Water;
        public float Mana;
        public float DiseasePressure;
        public float Stability;
        public float Education;
        public float Faith;
        public float WeatherFactor;
        public string LastEventSummary;
        public EventObservation[] ActiveEvents = Array.Empty<EventObservation>();
    }

    /// <summary>Immutable event observation projected from RegionEvent.</summary>
    [Serializable]
    public sealed class EventObservation
    {
        public string EventId;
        public SimEventType EventType;
        public RegionId RegionId;
        public SimEventScope Scope;
        public int StartDay;
        public int Duration;
        public int EndDay;
        public float Severity;
        public bool IsActive;
    }
}
