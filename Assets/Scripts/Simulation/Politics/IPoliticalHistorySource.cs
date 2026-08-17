using System.Collections.Generic;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Politics
{
    /// <summary>
    /// Observation-facing politics API. P2-B History / Report may read this.
    /// P2-C v0.2 does not write into ObservationHistory / RegionHistoryBuffer.
    /// </summary>
    public interface IPoliticalHistorySource
    {
        IReadOnlyList<PoliticalRelation> GetRelations();
        PoliticalRelation FindRelation(RegionId a, RegionId b);
        IReadOnlyList<PoliticalHistoryEntry> GetHistory(RegionId a, RegionId b);
        IReadOnlyList<PoliticalHistoryEntry> GetDiplomaticHistory();
        IReadOnlyList<Treaty> GetTreaties();
    }

    /// <summary>
    /// Adapter so Observation can obtain politics without depending on simulation ticks.
    /// </summary>
    public static class PoliticalObservation
    {
        public static IPoliticalHistorySource FromWorld(WorldState state)
        {
            return state != null ? state.Politics : null;
        }
    }
}
