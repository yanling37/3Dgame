using System.Collections.Generic;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Ring-capable history of observation snapshots keyed by TotalDays.
    /// Presentation/graph/report layers sample this buffer — they must not re-simulate.
    /// </summary>
    public sealed class SimulationHistoryBuffer
    {
        readonly List<WorldObservationSnapshot> _samples;
        readonly int _capacity;

        public SimulationHistoryBuffer(int capacity = 4096)
        {
            _capacity = capacity < 32 ? 32 : capacity;
            _samples = new List<WorldObservationSnapshot>(_capacity);
        }

        public int Count => _samples.Count;

        public WorldObservationSnapshot Latest =>
            _samples.Count == 0 ? null : _samples[_samples.Count - 1];

        /// <summary>Append or replace sample for the same TotalDays (idempotent per day).</summary>
        public void Record(WorldObservationSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            if (_samples.Count > 0)
            {
                var last = _samples[_samples.Count - 1];
                if (last != null && last.TotalDays == snapshot.TotalDays)
                {
                    _samples[_samples.Count - 1] = snapshot;
                    return;
                }
            }

            if (_samples.Count >= _capacity)
            {
                _samples.RemoveAt(0);
            }

            _samples.Add(snapshot);
        }

        public void Clear() => _samples.Clear();

        /// <summary>Exact TotalDays match, or null if missing.</summary>
        public WorldObservationSnapshot TryGetExact(int totalDays)
        {
            for (int i = _samples.Count - 1; i >= 0; i--)
            {
                var s = _samples[i];
                if (s != null && s.TotalDays == totalDays)
                {
                    return s;
                }
            }

            return null;
        }

        /// <summary>
        /// Nearest sample at or before totalDays (for scrubbing charts when FF skipped days).
        /// </summary>
        public WorldObservationSnapshot SampleAtOrBefore(int totalDays)
        {
            WorldObservationSnapshot best = null;
            for (int i = 0; i < _samples.Count; i++)
            {
                var s = _samples[i];
                if (s == null)
                {
                    continue;
                }

                if (s.TotalDays <= totalDays)
                {
                    best = s;
                }
                else
                {
                    break;
                }
            }

            return best;
        }

        public IReadOnlyList<WorldObservationSnapshot> AllSamples => _samples;
    }
}
