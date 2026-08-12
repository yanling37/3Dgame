using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Stability / Education / Faith drift. Keeps Phase 1 social stats alive for P2-A.
    /// </summary>
    public static class SocietySystem
    {
        public static void TickDay(RegionState region, SimulationConfig config, System.Random rng)
        {
            if (region == null || config == null)
            {
                return;
            }

            float knowledge = region.Get(ResourceId.Knowledge);
            float faith = region.Get(ResourceId.Faith);
            region.Education = Mathf.Clamp01(Mathf.Lerp(
                region.Education,
                Mathf.Clamp01(knowledge / config.KnowledgeEducationDivisor),
                config.EducationLerp));
            region.FaithLevel = Mathf.Clamp01(Mathf.Lerp(
                region.FaithLevel,
                Mathf.Clamp01(faith / config.FaithLevelDivisor),
                config.FaithLerp));

            if (rng != null
                && rng.NextDouble() < config.UnrestChance
                && region.Stability < config.UnrestStabilityThreshold)
            {
                region.LastEvent = "地方骚乱传闻";
                region.Stability = Mathf.Max(0.1f, region.Stability - config.UnrestStabilityLoss);
            }

            if (float.IsNaN(region.Stability) || float.IsInfinity(region.Stability))
            {
                region.Stability = 0.5f;
            }
        }
    }
}
