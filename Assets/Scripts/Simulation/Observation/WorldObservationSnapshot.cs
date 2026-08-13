using System;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Immutable world observation captured from Simulation State at one moment.
    /// </summary>
    public sealed class WorldObservationSnapshot
    {
        public static readonly WorldObservationSnapshot Empty = new WorldObservationSnapshot(
            1,
            1,
            0,
            SeasonId.Spring,
            false,
            Array.Empty<RegionObservationSnapshot>());

        public WorldObservationSnapshot(
            int year,
            int dayOfYear,
            int totalDays,
            SeasonId season,
            bool haltedOnNumericError,
            RegionObservationSnapshot[] regions)
        {
            Year = year;
            DayOfYear = dayOfYear;
            TotalDays = totalDays;
            Season = season;
            HaltedOnNumericError = haltedOnNumericError;
            Regions = regions ?? Array.Empty<RegionObservationSnapshot>();
        }

        public int Year { get; }
        public int DayOfYear { get; }
        public int TotalDays { get; }
        public SeasonId Season { get; }
        public bool HaltedOnNumericError { get; }
        public RegionObservationSnapshot[] Regions { get; }

        public RegionObservationSnapshot Find(RegionId id)
        {
            for (int i = 0; i < Regions.Length; i++)
            {
                if (Regions[i] != null && Regions[i].RegionId == id)
                {
                    return Regions[i];
                }
            }

            return null;
        }
    }
}
