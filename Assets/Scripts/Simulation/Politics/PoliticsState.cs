using System;
using System.Collections.Generic;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Politics
{
    /// <summary>
    /// World-owned undirected political graph plus directed diplomatic history and treaty placeholders.
    /// Not ticked by DailySimulation / FastForward in v0.2.
    /// </summary>
    [Serializable]
    public class PoliticsState : IPoliticalHistorySource
    {
        public PoliticalConfig Config = PoliticalConfig.CreateDefault();
        public List<PoliticalRelation> Relations = new List<PoliticalRelation>();
        public List<PoliticalHistoryEntry> DiplomaticHistory = new List<PoliticalHistoryEntry>();
        public List<Treaty> Treaties = new List<Treaty>();

        public IReadOnlyList<PoliticalRelation> GetRelations()
        {
            return Relations ?? (IReadOnlyList<PoliticalRelation>)Array.Empty<PoliticalRelation>();
        }

        public PoliticalRelation FindRelation(RegionId a, RegionId b)
        {
            if (Relations == null || a == b)
            {
                return null;
            }

            PoliticsSystem.Canonical(a, b, out RegionId source, out RegionId target);
            for (int i = 0; i < Relations.Count; i++)
            {
                var relation = Relations[i];
                if (relation != null && relation.SourceRegionId == source && relation.TargetRegionId == target)
                {
                    return relation;
                }
            }

            return null;
        }

        public IReadOnlyList<PoliticalHistoryEntry> GetHistory(RegionId a, RegionId b)
        {
            var relation = FindRelation(a, b);
            if (relation == null || relation.History == null)
            {
                return Array.Empty<PoliticalHistoryEntry>();
            }

            return relation.History;
        }

        public IReadOnlyList<PoliticalHistoryEntry> GetDiplomaticHistory()
        {
            return DiplomaticHistory ?? (IReadOnlyList<PoliticalHistoryEntry>)Array.Empty<PoliticalHistoryEntry>();
        }

        public IReadOnlyList<Treaty> GetTreaties()
        {
            return Treaties ?? (IReadOnlyList<Treaty>)Array.Empty<Treaty>();
        }

        public PoliticsState Clone()
        {
            var copy = new PoliticsState
            {
                Config = Config != null ? Config.Clone() : PoliticalConfig.CreateDefault(),
                Relations = new List<PoliticalRelation>(),
                DiplomaticHistory = new List<PoliticalHistoryEntry>(),
                Treaties = new List<Treaty>()
            };

            if (Relations != null)
            {
                for (int i = 0; i < Relations.Count; i++)
                {
                    if (Relations[i] != null)
                    {
                        copy.Relations.Add(Relations[i].Clone());
                    }
                }
            }

            if (DiplomaticHistory != null)
            {
                for (int i = 0; i < DiplomaticHistory.Count; i++)
                {
                    if (DiplomaticHistory[i] != null)
                    {
                        copy.DiplomaticHistory.Add(DiplomaticHistory[i].Clone());
                    }
                }
            }

            if (Treaties != null)
            {
                for (int i = 0; i < Treaties.Count; i++)
                {
                    if (Treaties[i] != null)
                    {
                        copy.Treaties.Add(Treaties[i].Clone());
                    }
                }
            }

            return copy;
        }
    }
}
