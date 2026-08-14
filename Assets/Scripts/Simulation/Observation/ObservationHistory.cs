using System;
using System.Collections.Generic;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// World history recorded from ObservationSnapshot ticks.
    /// Simulation → ObservationHost → ObservationSnapshot → History.
    /// </summary>
    public sealed class ObservationHistory
    {
        static readonly RegionId[] DefaultRegions =
        {
            RegionId.Theocracy,
            RegionId.Empire,
            RegionId.Sea
        };

        static readonly int[] PreferredAxisDays = { 1, 30, 90, 180, 270, 360 };

        readonly Dictionary<RegionId, RegionHistoryBuffer> _buffers =
            new Dictionary<RegionId, RegionHistoryBuffer>();

        int _lastTotalDays = -1;
        int _totalSampleWrites;

        public int Revision { get; private set; }
        public int LastTotalDays => _lastTotalDays < 0 ? 0 : _lastTotalDays;
        public int TotalSampleWrites => _totalSampleWrites;

        public RegionHistoryBuffer Buffer(RegionId regionId)
        {
            RegionHistoryBuffer buffer;
            if (!_buffers.TryGetValue(regionId, out buffer))
            {
                buffer = new RegionHistoryBuffer(regionId);
                _buffers[regionId] = buffer;
            }

            return buffer;
        }

        public int Count(RegionId regionId)
        {
            RegionHistoryBuffer buffer;
            return _buffers.TryGetValue(regionId, out buffer) ? buffer.Count : 0;
        }

        public int TotalEntryCount()
        {
            int n = 0;
            foreach (var pair in _buffers)
            {
                n += pair.Value.Count;
            }

            return n;
        }

        public HistorySample Find(RegionId regionId, int totalDays)
        {
            return Buffer(regionId).FindByTotalDays(totalDays);
        }

        public void Clear()
        {
            foreach (var pair in _buffers)
            {
                pair.Value.Clear();
            }

            _lastTotalDays = -1;
            _totalSampleWrites = 0;
            Revision++;
        }

        public void Record(WorldObservationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Regions == null || snapshot.Regions.Length == 0)
            {
                return;
            }

            if (_lastTotalDays >= 0 && snapshot.TotalDays < _lastTotalDays)
            {
                Clear();
            }

            for (int i = 0; i < snapshot.Regions.Length; i++)
            {
                var region = snapshot.Regions[i];
                if (region == null)
                {
                    continue;
                }

                var sample = new HistorySample(
                    snapshot.Year,
                    snapshot.DayOfYear,
                    snapshot.TotalDays,
                    snapshot.Season,
                    region);
                Buffer(region.RegionId).AppendOrReplace(sample);
                _totalSampleWrites++;
            }

            _lastTotalDays = snapshot.TotalDays;
            Revision++;
        }

        public TrendSeries Query(
            RegionId regionId,
            HistoryMetric metric,
            HistoryTimeRange range,
            int maxPlotPoints = 0)
        {
            var buffer = Buffer(regionId);
            if (buffer.Count == 0)
            {
                return new TrendSeries(
                    regionId,
                    metric,
                    range,
                    Array.Empty<HistorySample>(),
                    Array.Empty<TrendPlotPoint>(),
                    Array.Empty<TrendEventMarker>(),
                    Array.Empty<TrendAxisLabel>(),
                    HistoryMetrics.WindowDays(range),
                    0,
                    "No history");
            }

            var all = buffer.Samples;
            int latest = all[all.Count - 1].TotalDays;
            int earliest = all[0].TotalDays;
            int window = HistoryMetrics.WindowDays(range);
            int rangeStart = earliest;
            if (window > 0)
            {
                int requestedStart = latest - window + 1;
                rangeStart = requestedStart > earliest ? requestedStart : earliest;
            }

            int startIndex = 0;
            while (startIndex < all.Count && all[startIndex].TotalDays < rangeStart)
            {
                startIndex++;
            }

            int sliceCount = all.Count - startIndex;
            if (sliceCount <= 0)
            {
                startIndex = all.Count - 1;
                sliceCount = 1;
            }

            var samples = new HistorySample[sliceCount];
            for (int i = 0; i < sliceCount; i++)
            {
                samples[i] = all[startIndex + i];
            }

            var plot = BuildPlot(samples, metric, maxPlotPoints);
            var markers = BuildMarkers(buffer, regionId, samples[0].TotalDays, samples[samples.Length - 1].TotalDays);
            var labels = BuildAxisLabels(samples);
            string actual = HistoryMetrics.FormatTime(samples[0].Year, samples[0].DayOfYear)
                + " – "
                + HistoryMetrics.FormatTime(samples[samples.Length - 1].Year, samples[samples.Length - 1].DayOfYear)
                + " ("
                + samples.Length
                + " samples)";

            return new TrendSeries(
                regionId,
                metric,
                range,
                samples,
                plot,
                markers,
                labels,
                window,
                latest,
                actual);
        }

        public bool HasNonFiniteOrNegative(out string reason)
        {
            foreach (var pair in _buffers)
            {
                var samples = pair.Value.Samples;
                for (int i = 0; i < samples.Count; i++)
                {
                    var r = samples[i].Region;
                    if (IsBad(r.Population, "Population", pair.Key, samples[i].TotalDays, out reason)) return true;
                    if (IsBad(r.LastCarryingCapacity, "CarryingCapacity", pair.Key, samples[i].TotalDays, out reason)) return true;
                    if (IsBad(r.Food, "Food", pair.Key, samples[i].TotalDays, out reason)) return true;
                    if (IsBad(r.Water, "Water", pair.Key, samples[i].TotalDays, out reason)) return true;
                    if (IsBad(r.Wood, "Wood", pair.Key, samples[i].TotalDays, out reason)) return true;
                    if (IsBad(r.Mineral, "Mineral", pair.Key, samples[i].TotalDays, out reason)) return true;
                    if (IsBad(r.Magic, "Magic", pair.Key, samples[i].TotalDays, out reason)) return true;
                    if (IsBad(r.Disease, "Disease", pair.Key, samples[i].TotalDays, out reason)) return true;
                    if (IsBad(r.Stability, "Stability", pair.Key, samples[i].TotalDays, out reason)) return true;
                    if (IsBad(r.Education, "Education", pair.Key, samples[i].TotalDays, out reason)) return true;
                    if (IsBad(r.Faith, "Faith", pair.Key, samples[i].TotalDays, out reason)) return true;
                }
            }

            reason = null;
            return false;
        }

        static bool IsBad(float value, string name, RegionId region, int day, out string reason)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                reason = region + " " + name + " non-finite at TotalDays " + day + " = " + value;
                return true;
            }

            if (value < 0f)
            {
                reason = region + " " + name + " negative at TotalDays " + day + " = " + value;
                return true;
            }

            reason = null;
            return false;
        }

        static TrendPlotPoint[] BuildPlot(HistorySample[] samples, HistoryMetric metric, int maxPlotPoints)
        {
            int[] indices = SelectPlotIndices(samples, maxPlotPoints);
            var points = new TrendPlotPoint[indices.Length];
            for (int i = 0; i < indices.Length; i++)
            {
                var s = samples[indices[i]];
                points[i] = new TrendPlotPoint(s.TotalDays, s.Read(metric), s.Year, s.DayOfYear);
            }

            return points;
        }

        static int[] SelectPlotIndices(HistorySample[] samples, int maxPlotPoints)
        {
            int count = samples.Length;
            if (maxPlotPoints <= 0 || count <= maxPlotPoints)
            {
                var all = new int[count];
                for (int i = 0; i < count; i++)
                {
                    all[i] = i;
                }

                return all;
            }

            var set = new SortedSet<int> { 0, count - 1 };
            float step = (count - 1) / (float)(maxPlotPoints - 1);
            for (int i = 1; i < maxPlotPoints - 1; i++)
            {
                int idx = (int)(i * step + 0.5f);
                if (idx < 0) idx = 0;
                if (idx >= count) idx = count - 1;
                set.Add(idx);
            }

            var result = new int[set.Count];
            set.CopyTo(result);
            return result;
        }

        static TrendEventMarker[] BuildMarkers(
            RegionHistoryBuffer buffer,
            RegionId regionId,
            int rangeStart,
            int rangeEnd)
        {
            var list = new List<TrendEventMarker>(buffer.Events.Count);
            for (int i = 0; i < buffer.Events.Count; i++)
            {
                var evt = buffer.Events[i];
                if (evt == null || evt.RegionId != regionId || !evt.IsTrendMarker)
                {
                    continue;
                }

                if (!evt.Overlaps(rangeStart, rangeEnd))
                {
                    continue;
                }

                int markerDay = evt.StartDay;
                if (markerDay < rangeStart)
                {
                    markerDay = rangeStart;
                }

                if (markerDay > rangeEnd)
                {
                    markerDay = rangeEnd;
                }

                list.Add(new TrendEventMarker(evt, markerDay));
            }

            return list.ToArray();
        }

        static TrendAxisLabel[] BuildAxisLabels(HistorySample[] samples)
        {
            if (samples.Length == 0)
            {
                return Array.Empty<TrendAxisLabel>();
            }

            var labels = new List<TrendAxisLabel>(8);
            AddLabel(labels, samples[0]);

            for (int i = 0; i < samples.Length; i++)
            {
                var s = samples[i];
                if (IsPreferredAxisDay(s.DayOfYear) || IsPreferredAxisDay(s.TotalDays))
                {
                    AddLabel(labels, s);
                }
            }

            AddLabel(labels, samples[samples.Length - 1]);

            if (labels.Count > 8)
            {
                var reduced = new List<TrendAxisLabel>(8)
                {
                    labels[0],
                    labels[labels.Count - 1]
                };
                int stride = (labels.Count - 1) / 5;
                if (stride < 1)
                {
                    stride = 1;
                }

                for (int i = stride; i < labels.Count - 1; i += stride)
                {
                    AddLabel(reduced, labels[i]);
                }

                AddLabel(reduced, labels[labels.Count - 1]);
                return reduced.ToArray();
            }

            return labels.ToArray();
        }

        static bool IsPreferredAxisDay(int day)
        {
            for (int i = 0; i < PreferredAxisDays.Length; i++)
            {
                if (PreferredAxisDays[i] == day)
                {
                    return true;
                }
            }

            return false;
        }

        static void AddLabel(List<TrendAxisLabel> labels, HistorySample sample)
        {
            AddLabel(labels, new TrendAxisLabel(sample.TotalDays, sample.Year, sample.DayOfYear));
        }

        static void AddLabel(List<TrendAxisLabel> labels, TrendAxisLabel label)
        {
            for (int i = 0; i < labels.Count; i++)
            {
                if (labels[i].TotalDays == label.TotalDays)
                {
                    return;
                }
            }

            labels.Add(label);
        }

        public static RegionId[] AllRegions => DefaultRegions;
    }
}
