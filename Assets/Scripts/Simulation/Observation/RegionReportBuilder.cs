using System;
using System.Collections.Generic;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Builds a region report from HistoryBuffer / ObservationSnapshot only.
    /// Never recomputes population, resources, disease, or stability.
    /// </summary>
    public static class RegionReportBuilder
    {
        public const float DiseaseWatch = 0.25f;

        public static RegionReport Build(
            ObservationHistory history,
            WorldObservationSnapshot current,
            RegionId regionId,
            ReportPeriod period)
        {
            if (history == null)
            {
                return Empty(regionId, period);
            }

            var latest = history.Latest(regionId);
            var snap = current != null ? current.Find(regionId) : null;
            if (latest == null && snap == null)
            {
                return Empty(regionId, period);
            }

            int year = latest != null ? latest.Year : current.Year;
            int dayOfYear = latest != null ? latest.DayOfYear : current.DayOfYear;
            var season = latest != null ? latest.Season : current.Season;
            int totalDays = latest != null ? latest.TotalDays : current.TotalDays;
            string name = snap != null ? snap.DisplayName : (latest != null ? latest.Region.DisplayName : regionId.ToString());
            var now = latest != null ? latest.Region : snap;

            int periodDays = RegionReport.PeriodDays(period);
            int target = totalDays - periodDays;
            var prevSample = history.FindAtOrBefore(regionId, target);
            bool hasPrevious = prevSample != null
                && prevSample.TotalDays != totalDays
                && (totalDays - prevSample.TotalDays) >= periodDays / 2;

            var lines = new ReportMetricLine[RegionReport.Metrics.Length];
            for (int i = 0; i < RegionReport.Metrics.Length; i++)
            {
                var metric = RegionReport.Metrics[i];
                float cur = HistoryMetrics.Read(now, metric);
                float prev = hasPrevious ? prevSample.Read(metric) : 0f;
                bool warning = metric == HistoryMetric.Disease && cur >= DiseaseWatch;
                lines[i] = new ReportMetricLine(metric, cur, prev, hasPrevious, warning);
            }

            int rangeStart = hasPrevious ? prevSample.TotalDays : (totalDays - periodDays);
            if (rangeStart < 0) rangeStart = 0;
            var events = CollectEvents(history.Buffer(regionId), regionId, rangeStart, totalDays);
            var turns = CollectTurningPoints(history.Buffer(regionId), rangeStart, totalDays);

            return new RegionReport(
                regionId,
                name,
                year,
                dayOfYear,
                season,
                totalDays,
                period,
                hasPrevious,
                hasPrevious ? prevSample.TotalDays : -1,
                lines,
                events,
                turns);
        }

        static RegionReport Empty(RegionId regionId, ReportPeriod period)
        {
            return new RegionReport(
                regionId,
                regionId.ToString(),
                1,
                1,
                SeasonId.Spring,
                0,
                period,
                false,
                -1,
                System.Array.Empty<ReportMetricLine>(),
                ObservationEventRecord.None,
                System.Array.Empty<ReportTurningPoint>());
        }

        static ObservationEventRecord[] CollectEvents(
            RegionHistoryBuffer buffer,
            RegionId regionId,
            int rangeStart,
            int rangeEnd)
        {
            var list = new List<ObservationEventRecord>(8);
            var events = buffer.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var evt = events[i];
                if (evt == null || evt.RegionId != regionId || !evt.IsTrendMarker)
                {
                    continue;
                }

                if (evt.Overlaps(rangeStart, rangeEnd))
                {
                    list.Add(evt);
                }
            }

            return list.ToArray();
        }

        static ReportTurningPoint[] CollectTurningPoints(
            RegionHistoryBuffer buffer,
            int rangeStart,
            int rangeEnd)
        {
            var samples = buffer.Samples;
            int start = 0;
            while (start < samples.Count && samples[start].TotalDays < rangeStart)
            {
                start++;
            }

            int end = samples.Count - 1;
            while (end >= start && samples[end].TotalDays > rangeEnd)
            {
                end--;
            }

            if (end < start)
            {
                return System.Array.Empty<ReportTurningPoint>();
            }

            int minPopI = start;
            int maxDisI = start;
            float minPop = samples[start].Region.Population;
            float maxDis = samples[start].Region.Disease;
            for (int i = start + 1; i <= end; i++)
            {
                float pop = samples[i].Region.Population;
                float dis = samples[i].Region.Disease;
                if (pop < minPop)
                {
                    minPop = pop;
                    minPopI = i;
                }

                if (dis > maxDis)
                {
                    maxDis = dis;
                    maxDisI = i;
                }
            }

            var list = new List<ReportTurningPoint>(4);
            var minS = samples[minPopI];
            list.Add(new ReportTurningPoint(
                minS.TotalDays, minS.Year, minS.DayOfYear,
                HistoryMetric.Population, minS.Region.Population,
                "Population low"));
            var disS = samples[maxDisI];
            list.Add(new ReportTurningPoint(
                disS.TotalDays, disS.Year, disS.DayOfYear,
                HistoryMetric.Disease, disS.Region.Disease,
                "Disease peak"));
            return list.ToArray();
        }
    }
}
