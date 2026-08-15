using System.Collections.Generic;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Per-region history buffer. Samples are appended on simulation ticks only.
    /// Entries are never discarded except on world Reset.
    /// </summary>
    public sealed class RegionHistoryBuffer
    {
        readonly List<HistorySample> _samples = new List<HistorySample>(512);
        readonly List<ObservationEventRecord> _events = new List<ObservationEventRecord>(16);
        readonly HashSet<string> _eventIds = new HashSet<string>();

        public RegionHistoryBuffer(RegionId regionId)
        {
            RegionId = regionId;
        }

        public RegionId RegionId { get; }
        public int Count => _samples.Count;
        public IReadOnlyList<HistorySample> Samples => _samples;
        public IReadOnlyList<ObservationEventRecord> Events => _events;

        public HistorySample Last => _samples.Count > 0 ? _samples[_samples.Count - 1] : null;
        public HistorySample First => _samples.Count > 0 ? _samples[0] : null;

        public HistorySample FindAtOrBefore(int totalDays)
        {
            if (_samples.Count == 0 || _samples[0].TotalDays > totalDays)
            {
                return null;
            }

            int lo = 0;
            int hi = _samples.Count - 1;
            int best = 0;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) / 2);
                int day = _samples[mid].TotalDays;
                if (day == totalDays)
                {
                    return _samples[mid];
                }

                if (day < totalDays)
                {
                    best = mid;
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return _samples[best];
        }

        public void AppendOrReplace(HistorySample sample)
        {
            if (sample == null || sample.RegionId != RegionId)
            {
                return;
            }

            if (_samples.Count > 0 && _samples[_samples.Count - 1].TotalDays == sample.TotalDays)
            {
                _samples[_samples.Count - 1] = sample;
            }
            else
            {
                _samples.Add(sample);
            }

            RememberEvents(sample.Region);
        }

        public HistorySample FindByTotalDays(int totalDays)
        {
            int lo = 0;
            int hi = _samples.Count - 1;
            while (lo <= hi)
            {
                int mid = lo + ((hi - lo) / 2);
                int day = _samples[mid].TotalDays;
                if (day == totalDays)
                {
                    return _samples[mid];
                }

                if (day < totalDays)
                {
                    lo = mid + 1;
                }
                else
                {
                    hi = mid - 1;
                }
            }

            return null;
        }

        public void Clear()
        {
            _samples.Clear();
            _events.Clear();
            _eventIds.Clear();
        }

        void RememberEvents(RegionObservationSnapshot region)
        {
            if (region?.Events == null)
            {
                return;
            }

            for (int i = 0; i < region.Events.Length; i++)
            {
                var evt = region.Events[i];
                if (evt == null || !evt.IsTrendMarker || evt.RegionId != RegionId)
                {
                    continue;
                }

                string key = string.IsNullOrEmpty(evt.EventId)
                    ? evt.EventType + ":" + evt.RegionId + ":" + evt.StartDay
                    : evt.EventId;
                if (_eventIds.Add(key))
                {
                    _events.Add(evt);
                }
            }
        }
    }
}
