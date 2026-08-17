using System;
using System.Collections.Generic;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Politics
{
    /// <summary>
    /// Undirected pair: SourceRegionId ↔ TargetRegionId (canonical Source &lt; Target).
    /// Diplomatic actions record a directed Source → Target but write this single value.
    /// RelationState is derived from RelationValue; never assign it from an action type.
    /// Mutate RelationValue only through <see cref="PoliticsSystem.ApplyDiplomaticAction"/> (or reset).
    /// </summary>
    [Serializable]
    public class PoliticalRelation
    {
        public RegionId SourceRegionId;
        public RegionId TargetRegionId;
        public float RelationValue;
        public PoliticalRelationState RelationState;
        public int LastChangedDay;
        public List<PoliticalHistoryEntry> History = new List<PoliticalHistoryEntry>();

        public string PairLabel => SourceRegionId + " ↔ " + TargetRegionId;

        public bool Matches(RegionId a, RegionId b)
        {
            PoliticsSystem.Canonical(a, b, out RegionId source, out RegionId target);
            return SourceRegionId == source && TargetRegionId == target;
        }

        public PoliticalRelation Clone()
        {
            var copy = new PoliticalRelation
            {
                SourceRegionId = SourceRegionId,
                TargetRegionId = TargetRegionId,
                RelationValue = RelationValue,
                RelationState = RelationState,
                LastChangedDay = LastChangedDay,
                History = new List<PoliticalHistoryEntry>()
            };

            if (History != null)
            {
                for (int i = 0; i < History.Count; i++)
                {
                    var entry = History[i];
                    if (entry != null)
                    {
                        copy.History.Add(entry.Clone());
                    }
                }
            }

            return copy;
        }
    }
}
