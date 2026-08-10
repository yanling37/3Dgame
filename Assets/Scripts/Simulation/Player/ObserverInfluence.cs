using System;
using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Player
{
    /// <summary>
    /// Phase 1: observer may only nudge probabilities, never set absolute outcomes.
    /// </summary>
    [Serializable]
    public class ObserverInfluence
    {
        [Range(0.5f, 1.5f)] public float FertilityBlessing = 1f;
        [Range(0.5f, 1.5f)] public float HarvestBlessing = 1f;
        [Range(0.5f, 1.5f)] public float DiseaseCurse = 1f; // >1 increases disease pressure growth
        [Range(0.5f, 1.5f)] public float StabilityBlessing = 1f;
        public RegionId? FocusRegion;

        public void ResetSoft()
        {
            FertilityBlessing = 1f;
            HarvestBlessing = 1f;
            DiseaseCurse = 1f;
            StabilityBlessing = 1f;
        }

        public float RegionMultiplier(RegionId region, float value)
        {
            if (FocusRegion.HasValue && FocusRegion.Value != region)
            {
                // Mild global bleed: 30% of focused blessing applies elsewhere.
                return Mathf.Lerp(1f, value, 0.3f);
            }

            return value;
        }
    }
}
