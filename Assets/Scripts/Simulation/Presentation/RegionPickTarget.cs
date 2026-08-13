using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;
using UnityEngine;

namespace DivineWorld.Simulation.Presentation
{
    /// <summary>
    /// Click target on a region totem. Selects that region on ObservationHost.
    /// </summary>
    public class RegionPickTarget : MonoBehaviour
    {
        ObservationHost _host;
        RegionId _regionId;

        public void Bind(ObservationHost host, RegionId regionId)
        {
            _host = host;
            _regionId = regionId;
        }

        void OnMouseDown()
        {
            if (_host != null)
            {
                _host.SelectRegion(_regionId);
            }
        }
    }
}
