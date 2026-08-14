using System;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    public enum HistoryMetric
    {
        Population = 0,
        Food = 1,
        Water = 2,
        Disease = 3,
        Stability = 4,
        Magic = 5
    }

    public enum HistoryTimeRange
    {
        Recent30Days = 0,
        Recent90Days = 1,
        Recent1Year = 2,
        AllHistory = 3
    }

    /// <summary>
    /// One captured observation tick for a single region. Values are copied, never recomputed.
    /// </summary>
    public sealed class HistorySample
    {
        public HistorySample(
            int year,
            int dayOfYear,
            int totalDays,
            SeasonId season,
            RegionObservationSnapshot region)
        {
            Year = year;
            DayOfYear = dayOfYear;
            TotalDays = totalDays;
            Season = season;
            Region = region ?? throw new ArgumentNullException(nameof(region));
        }

        public int Year { get; }
        public int DayOfYear { get; }
        public int TotalDays { get; }
        public SeasonId Season { get; }
        public RegionObservationSnapshot Region { get; }
        public RegionId RegionId => Region.RegionId;

        public float Read(HistoryMetric metric) => HistoryMetrics.Read(Region, metric);

        public string TimeLabel => HistoryMetrics.FormatTime(Year, DayOfYear);
    }

    public static class HistoryMetrics
    {
        public static int WindowDays(HistoryTimeRange range)
        {
            switch (range)
            {
                case HistoryTimeRange.Recent30Days: return 30;
                case HistoryTimeRange.Recent90Days: return 90;
                case HistoryTimeRange.Recent1Year: return 360;
                default: return 0;
            }
        }

        public static string DisplayName(HistoryMetric metric)
        {
            switch (metric)
            {
                case HistoryMetric.Population: return "Population";
                case HistoryMetric.Food: return "Food";
                case HistoryMetric.Water: return "Water";
                case HistoryMetric.Disease: return "Disease";
                case HistoryMetric.Stability: return "Stability";
                case HistoryMetric.Magic: return "Magic";
                default: return metric.ToString();
            }
        }

        public static string DisplayName(HistoryTimeRange range)
        {
            switch (range)
            {
                case HistoryTimeRange.Recent30Days: return "Recent 30 Days";
                case HistoryTimeRange.Recent90Days: return "Recent 90 Days";
                case HistoryTimeRange.Recent1Year: return "Recent 1 Year";
                default: return "All History";
            }
        }

        public static string FormatTime(int year, int dayOfYear)
        {
            return "Year " + year + " Day " + dayOfYear;
        }

        public static float Read(RegionObservationSnapshot region, HistoryMetric metric)
        {
            if (region == null)
            {
                return 0f;
            }

            switch (metric)
            {
                case HistoryMetric.Population: return region.Population;
                case HistoryMetric.Food: return region.Food;
                case HistoryMetric.Water: return region.Water;
                case HistoryMetric.Disease: return region.Disease;
                case HistoryMetric.Stability: return region.Stability;
                case HistoryMetric.Magic: return region.Magic;
                default: return 0f;
            }
        }
    }

    public sealed class TrendEventMarker
    {
        public TrendEventMarker(ObservationEventRecord record, int markerTotalDays)
        {
            Record = record ?? throw new ArgumentNullException(nameof(record));
            MarkerTotalDays = markerTotalDays;
        }

        public ObservationEventRecord Record { get; }
        public int MarkerTotalDays { get; }
        public RegionId RegionId => Record.RegionId;
        public SimEventType EventType => Record.EventType;
        public string Label => ObservationEventRecord.DisplayName(Record.EventType);
        public string TimeLabel => "Day " + MarkerTotalDays;
    }

    public sealed class TrendAxisLabel
    {
        public TrendAxisLabel(int totalDays, int year, int dayOfYear)
        {
            TotalDays = totalDays;
            Year = year;
            DayOfYear = dayOfYear;
            Text = HistoryMetrics.FormatTime(year, dayOfYear);
        }

        public int TotalDays { get; }
        public int Year { get; }
        public int DayOfYear { get; }
        public string Text { get; }
    }

    public sealed class TrendPlotPoint
    {
        public TrendPlotPoint(int totalDays, float value, int year, int dayOfYear)
        {
            TotalDays = totalDays;
            Value = value;
            Year = year;
            DayOfYear = dayOfYear;
        }

        public int TotalDays { get; }
        public float Value { get; }
        public int Year { get; }
        public int DayOfYear { get; }
    }

    /// <summary>
    /// Query result for one region / metric / time range. Does not own or mutate HistoryBuffer.
    /// </summary>
    public sealed class TrendSeries
    {
        public static readonly TrendSeries Empty = new TrendSeries(
            RegionId.Theocracy,
            HistoryMetric.Population,
            HistoryTimeRange.AllHistory,
            Array.Empty<HistorySample>(),
            Array.Empty<TrendPlotPoint>(),
            Array.Empty<TrendEventMarker>(),
            Array.Empty<TrendAxisLabel>(),
            0,
            0,
            "No history");

        public TrendSeries(
            RegionId regionId,
            HistoryMetric metric,
            HistoryTimeRange requestedRange,
            HistorySample[] samples,
            TrendPlotPoint[] plotPoints,
            TrendEventMarker[] eventMarkers,
            TrendAxisLabel[] axisLabels,
            int requestedWindowDays,
            int latestTotalDays,
            string actualRangeLabel)
        {
            RegionId = regionId;
            Metric = metric;
            RequestedRange = requestedRange;
            Samples = samples ?? Array.Empty<HistorySample>();
            PlotPoints = plotPoints ?? Array.Empty<TrendPlotPoint>();
            EventMarkers = eventMarkers ?? Array.Empty<TrendEventMarker>();
            AxisLabels = axisLabels ?? Array.Empty<TrendAxisLabel>();
            RequestedWindowDays = requestedWindowDays;
            LatestTotalDays = latestTotalDays;
            ActualRangeLabel = actualRangeLabel ?? string.Empty;
        }

        public RegionId RegionId { get; }
        public HistoryMetric Metric { get; }
        public HistoryTimeRange RequestedRange { get; }
        public HistorySample[] Samples { get; }
        public TrendPlotPoint[] PlotPoints { get; }
        public TrendEventMarker[] EventMarkers { get; }
        public TrendAxisLabel[] AxisLabels { get; }
        public int RequestedWindowDays { get; }
        public int LatestTotalDays { get; }
        public string ActualRangeLabel { get; }

        public int SampleCount => Samples.Length;
        public bool HasData => Samples.Length > 0;

        public int FirstTotalDays => HasData ? Samples[0].TotalDays : 0;
        public int LastTotalDays => HasData ? Samples[Samples.Length - 1].TotalDays : 0;
        public HistorySample First => HasData ? Samples[0] : null;
        public HistorySample Last => HasData ? Samples[Samples.Length - 1] : null;
    }
}
