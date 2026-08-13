using System;
using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Unity host: SimulationWorld → ObservationSession → snapshot events.
    /// Presentation reads snapshots from here, not by re-deriving population.
    /// </summary>
    public class ObservationHost : MonoBehaviour
    {
        [SerializeField] SimulationWorld world;

        readonly ObservationSession _session = new ObservationSession();
        bool _bound;

        public ObservationSession Session => _session;
        public WorldObservationSnapshot Current => _session.Current;

        public event Action<WorldObservationSnapshot> OnSnapshotUpdated
        {
            add => _session.Updated += value;
            remove => _session.Updated -= value;
        }

        public void Bind(SimulationWorld simulationWorld)
        {
            if (world != null && _bound)
            {
                world.OnDayAdvanced -= OnDayAdvanced;
                _bound = false;
            }

            world = simulationWorld;
            if (world != null)
            {
                world.OnDayAdvanced += OnDayAdvanced;
                _bound = true;
                Recapture();
            }
        }

        public WorldObservationSnapshot Recapture()
        {
            return _session.Capture(world != null ? world.State : null);
        }

        void OnDayAdvanced(WorldState _)
        {
            Recapture();
        }

        void Start()
        {
            if (world == null)
            {
                world = FindObjectOfType<SimulationWorld>();
            }

            if (world != null && !_bound)
            {
                Bind(world);
            }
        }

        void OnDestroy()
        {
            if (world != null && _bound)
            {
                world.OnDayAdvanced -= OnDayAdvanced;
                _bound = false;
            }
        }
    }
}
