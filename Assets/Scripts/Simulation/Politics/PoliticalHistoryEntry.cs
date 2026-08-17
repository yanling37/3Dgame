using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Politics
{
    /// <summary>
    /// One diplomatic act belonging to an undirected pair.
    /// Source/Target are the action direction (Empire → Theocracy), not canonical pair order.
    /// Observation may read this; it is not stored in ObservationHistory.
    /// </summary>
    public sealed class PoliticalHistoryEntry
    {
        public int Day;
        public RegionId SourceRegionId;
        public RegionId TargetRegionId;
        public DiplomaticActionType ActionType;
        public float OldValue;
        public float Delta;
        public float NewValue;
        public string Reason;

        public PoliticalHistoryEntry Clone()
        {
            return new PoliticalHistoryEntry
            {
                Day = Day,
                SourceRegionId = SourceRegionId,
                TargetRegionId = TargetRegionId,
                ActionType = ActionType,
                OldValue = OldValue,
                Delta = Delta,
                NewValue = NewValue,
                Reason = Reason
            };
        }

        /// <summary>
        /// Observation-facing line, e.g. "Day 120  Empire → Theocracy  ImproveRelations  +10  +32 → +42  Reason: Diplomatic Gesture".
        /// </summary>
        public string ToObservationLine()
        {
            return "Day " + Day
                + "  " + SourceRegionId + " → " + TargetRegionId
                + "  " + ActionType
                + "  " + FormatSigned(Delta)
                + "  " + FormatSigned(OldValue) + " → " + FormatSigned(NewValue)
                + "  Reason: " + (string.IsNullOrEmpty(Reason) ? "(none)" : Reason);
        }

        static string FormatSigned(float value)
        {
            string body = value.ToString("0.##");
            if (value > 0f && body.Length > 0 && body[0] != '+')
            {
                return "+" + body;
            }

            return body;
        }
    }
}
