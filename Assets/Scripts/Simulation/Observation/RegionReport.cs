using System;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Same-metric comparison of Theocracy / Empire / Sea over one shared time window.
    /// </summary>
    public sealed class RegionCompare
    {
        public RegionCompare(
            HistoryMetric metric,
            HistoryTimeRange range,
            int rangeStart,
            int rangeEnd,
            TrendSeries[] series,
            string actualRangeLabel)
        {
            Metric = metric;
            Range = range;
            RangeStart = rangeStart;
            RangeEnd = rangeEnd;
            Series = series ?? Array.Empty<TrendSeries>();
            ActualRangeLabel = actualRangeLabel ?? string.Empty;
        }

        public HistoryMetric Metric { get; }
        public HistoryTimeRange Range { get; }
        public int RangeStart { get; }
        public int RangeEnd { get; }
        public TrendSeries[] Series { get; }
        public string ActualRangeLabel { get; }

        public bool HasData
        {
            get
            {
                for (int i = 0; i < Series.Length; i++)
                {
                    if (Series[i] != null && Series[i].HasData)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public TrendSeries For(RegionId regionId)
        {
            for (int i = 0; i < Series.Length; i++)
            {
                if (Series[i] != null && Series[i].RegionId == regionId)
                {
                    return Series[i];
                }
            }

            return TrendSeries.Empty;
        }

        public float Current(RegionId regionId)
        {
            var series = For(regionId);
            return series.HasData ? series.Last.Read(Metric) : 0f;
        }
    }

    public enum ReportPeriod
    {
        Season = 0,
        Year = 1
    }

    public enum ReportTrend
    {
        Flat = 0,
        Up = 1,
        Down = 2
    }

    public sealed class ReportMetricLine
    {
        public ReportMetricLine(
            HistoryMetric metric,
            float current,
            float previous,
            bool hasPrevious,
            bool warning)
        {
            Metric = metric;
            Current = current;
            Previous = previous;
            HasPrevious = hasPrevious;
            Warning = warning;
            Delta = hasPrevious ? current - previous : 0f;
            if (!hasPrevious)
            {
                Percent = 0f;
                Trend = ReportTrend.Flat;
            }
            else if (Math.Abs(previous) < 0.0001f)
            {
                Percent = current > previous ? 100f : 0f;
                Trend = current > previous + 0.0001f
                    ? ReportTrend.Up
                    : (current < previous - 0.0001f ? ReportTrend.Down : ReportTrend.Flat);
            }
            else
            {
                Percent = (current - previous) / Math.Abs(previous) * 100f;
                if (Percent > 0.5f) Trend = ReportTrend.Up;
                else if (Percent < -0.5f) Trend = ReportTrend.Down;
                else Trend = ReportTrend.Flat;
            }
        }

        public HistoryMetric Metric { get; }
        public float Current { get; }
        public float Previous { get; }
        public float Delta { get; }
        public float Percent { get; }
        public bool HasPrevious { get; }
        public ReportTrend Trend { get; }
        public bool Warning { get; }

        public string TrendMark
        {
            get
            {
                switch (Trend)
                {
                    case ReportTrend.Up: return "↑";
                    case ReportTrend.Down: return "↓";
                    default: return "→";
                }
            }
        }
    }

    public sealed class ReportTurningPoint
    {
        public ReportTurningPoint(int totalDays, int year, int dayOfYear, HistoryMetric metric, float value, string label)
        {
            TotalDays = totalDays;
            Year = year;
            DayOfYear = dayOfYear;
            Metric = metric;
            Value = value;
            Label = label ?? string.Empty;
        }

        public int TotalDays { get; }
        public int Year { get; }
        public int DayOfYear { get; }
        public HistoryMetric Metric { get; }
        public float Value { get; }
        public string Label { get; }
        public string TimeLabel => HistoryMetrics.FormatTime(Year, DayOfYear);
    }

    public sealed class RegionReport
    {
        public static readonly HistoryMetric[] Metrics =
        {
            HistoryMetric.Population,
            HistoryMetric.CarryingCapacity,
            HistoryMetric.Food,
            HistoryMetric.Water,
            HistoryMetric.Wood,
            HistoryMetric.Mineral,
            HistoryMetric.Magic,
            HistoryMetric.Disease,
            HistoryMetric.Stability,
            HistoryMetric.Education,
            HistoryMetric.Faith
        };

        public RegionReport(
            RegionId regionId,
            string displayName,
            int year,
            int dayOfYear,
            SeasonId season,
            int totalDays,
            ReportPeriod period,
            bool hasPrevious,
            int previousTotalDays,
            ReportMetricLine[] lines,
            ObservationEventRecord[] majorEvents,
            ReportTurningPoint[] turningPoints)
        {
            RegionId = regionId;
            DisplayName = displayName ?? regionId.ToString();
            Year = year;
            DayOfYear = dayOfYear;
            Season = season;
            TotalDays = totalDays;
            Period = period;
            HasPrevious = hasPrevious;
            PreviousTotalDays = previousTotalDays;
            Lines = lines ?? Array.Empty<ReportMetricLine>();
            MajorEvents = majorEvents ?? ObservationEventRecord.None;
            TurningPoints = turningPoints ?? Array.Empty<ReportTurningPoint>();
        }

        public RegionId RegionId { get; }
        public string DisplayName { get; }
        public int Year { get; }
        public int DayOfYear { get; }
        public SeasonId Season { get; }
        public int TotalDays { get; }
        public ReportPeriod Period { get; }
        public bool HasPrevious { get; }
        public int PreviousTotalDays { get; }
        public ReportMetricLine[] Lines { get; }
        public ObservationEventRecord[] MajorEvents { get; }
        public ReportTurningPoint[] TurningPoints { get; }

        public string Title
        {
            get
            {
                return Period == ReportPeriod.Season
                    ? DisplayName + " · Year " + Year + " " + Season + " Report"
                    : DisplayName + " · Year " + Year + " Report";
            }
        }

        public ReportMetricLine Line(HistoryMetric metric)
        {
            for (int i = 0; i < Lines.Length; i++)
            {
                if (Lines[i].Metric == metric)
                {
                    return Lines[i];
                }
            }

            return null;
        }

        public static int PeriodDays(ReportPeriod period)
        {
            return period == ReportPeriod.Season ? 90 : 360;
        }
    }
}
