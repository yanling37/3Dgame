using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Politics
{
    /// <summary>
    /// Placeholder incident kinds. P2-C v0.2 does not roll random diplomatic events.
    /// </summary>
    public enum DiplomaticIncidentType
    {
        Unspecified = 0,
        DiplomaticGesture = 1,
        BorderTension = 2
    }

    /// <summary>
    /// Diplomatic incident interface for v0.2: type, source, target, delta, reason, day.
    /// Apply through <see cref="PoliticsSystem.ApplyDiplomaticIncident"/>; never auto-generated.
    /// </summary>
    public sealed class DiplomaticIncident
    {
        public DiplomaticIncidentType Type;
        public RegionId SourceRegion;
        public RegionId TargetRegion;
        public float Delta;
        public string Reason;
        public int Day;

        public DiplomaticIncident Clone()
        {
            return new DiplomaticIncident
            {
                Type = Type,
                SourceRegion = SourceRegion,
                TargetRegion = TargetRegion,
                Delta = Delta,
                Reason = Reason,
                Day = Day
            };
        }
    }
}
