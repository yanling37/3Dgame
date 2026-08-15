using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Politics
{
    /// <summary>
    /// One relation change belonging to a single undirected pair.
    /// Observation / future P2-B reports may read this; it is not stored in ObservationHistory.
    /// </summary>
    public sealed class PoliticalHistoryEntry
    {
        public int Day;
        public float OldValue;
        public float NewValue;
        public string Reason;
        public RegionId SourceRegionId;
        public RegionId TargetRegionId;

        public PoliticalHistoryEntry Clone()
        {
            return new PoliticalHistoryEntry
            {
                Day = Day,
                OldValue = OldValue,
                NewValue = NewValue,
                Reason = Reason,
                SourceRegionId = SourceRegionId,
                TargetRegionId = TargetRegionId
            };
        }

        /// <summary>
        /// Stable Observation-facing line, e.g. "Day 120  Empire ↔ Sea  +20 → -10  Reason: Diplomatic Incident".
        /// </summary>
        public string ToObservationLine()
        {
            return "Day " + Day
                + "  " + SourceRegionId + " ↔ " + TargetRegionId
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
