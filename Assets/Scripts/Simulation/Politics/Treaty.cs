using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Politics
{
    /// <summary>
    /// Treaty kinds reserved in P2-C v0.2. Trade has no resource effect. Alliance has no military effect.
    /// </summary>
    public enum TreatyType
    {
        NonAggression = 0,
        Trade = 1,
        Alliance = 2
    }

    /// <summary>
    /// Treaty placeholder. Creation / expiry only; no war simulation and no resource trade.
    /// </summary>
    public sealed class Treaty
    {
        public TreatyType TreatyType;
        public RegionId SourceRegion;
        public RegionId TargetRegion;
        public int StartDay;
        public int EndDay;
        public bool Active;

        /// <summary>
        /// Calendar expiry uses <paramref name="day"/>; <see cref="Active"/> can also be cleared explicitly.
        /// EndDay &lt; 0 means no calendar expiry.
        /// </summary>
        public bool IsActiveAt(int day)
        {
            if (!Active)
            {
                return false;
            }

            if (day < StartDay)
            {
                return false;
            }

            if (EndDay >= 0 && day > EndDay)
            {
                return false;
            }

            return true;
        }

        public Treaty Clone()
        {
            return new Treaty
            {
                TreatyType = TreatyType,
                SourceRegion = SourceRegion,
                TargetRegion = TargetRegion,
                StartDay = StartDay,
                EndDay = EndDay,
                Active = Active
            };
        }
    }
}
