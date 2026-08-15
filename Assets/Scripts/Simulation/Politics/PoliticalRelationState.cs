namespace DivineWorld.Simulation.Politics
{
    /// <summary>
    /// Discrete diplomatic standing derived from <see cref="PoliticalRelation.RelationValue"/>.
    /// </summary>
    public enum PoliticalRelationState
    {
        Friendly = 0,
        Neutral = 1,
        Tense = 2,
        Hostile = 3,
        /// <summary>
        /// Reserved / NotImplemented. P2-C v0.1 never assigns this and has no war simulation.
        /// </summary>
        War = 4
    }

    /// <summary>
    /// War is a reserved state only. No army, battle, occupation, or casualty logic in v0.1.
    /// </summary>
    public static class WarReservation
    {
        public const bool Implemented = false;
        public const string Status = "Reserved / NotImplemented";
    }
}
