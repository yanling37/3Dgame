using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Politics
{
    /// <summary>
    /// Directed diplomatic act. The underlying <see cref="PoliticalRelation"/> stays undirected.
    /// Empire → Theocracy still writes the single Empire ↔ Theocracy value.
    /// </summary>
    public enum DiplomaticActionType
    {
        ImproveRelations = 0,
        WorsenRelations = 1,
        DiplomaticIncident = 2,
        Treaty = 3
    }

    /// <summary>
    /// Input to the single relation-mutation entry <see cref="PoliticsSystem.ApplyDiplomaticAction"/>.
    /// Must specify Source, Target, ActionType, Delta, Day, and Reason.
    /// </summary>
    public sealed class DiplomaticAction
    {
        public const string DefaultImproveReason = "Diplomatic Gesture";
        public const string DefaultWorsenReason = "Diplomatic Slight";
        public const string DefaultIncidentReason = "Diplomatic Incident";

        public RegionId SourceRegion;
        public RegionId TargetRegion;
        public DiplomaticActionType ActionType;
        public float Delta;
        public int Day;
        public string Reason;

        public static DiplomaticAction Create(
            RegionId source,
            RegionId target,
            DiplomaticActionType actionType,
            float delta,
            string reason)
        {
            return new DiplomaticAction
            {
                SourceRegion = source,
                TargetRegion = target,
                ActionType = actionType,
                Delta = delta,
                Reason = reason
            };
        }

        public DiplomaticAction Clone()
        {
            return new DiplomaticAction
            {
                SourceRegion = SourceRegion,
                TargetRegion = TargetRegion,
                ActionType = ActionType,
                Delta = Delta,
                Day = Day,
                Reason = Reason
            };
        }
    }
}
