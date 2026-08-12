using System;
using UnityEngine;

namespace DivineWorld.Simulation.Data
{
    /// <summary>
    /// Legacy / docs-facing resource type descriptor (Phase 2).
    /// Runtime lifecycle rules live in <see cref="ResourceTypeDefinition"/> / ResourceCatalog (ResourceModels).
    /// </summary>
    [Serializable]
    public class ResourceDefinition
    {
        public ResourceId Id;
        public string DisplayName;
        public bool CanSpoil;
        public float BaseSpoilRate;
        public float BaseConsumePerCapita;
        public float BaseProductionScale = 0.01f;
        public bool UsesLabor = true;
        public bool UsesTech = true;
        public bool UsesWeather = true;
        public bool UsesHarvestBlessing;
        public bool AffectsCarryingCapacity;
        public bool SeaSuppressed;
        public float SeaFallbackYield;

        // Seasonal production multipliers: Spring, Summer, Autumn, Winter
        public float[] SeasonalProduction = { 1f, 1f, 1f, 1f };

        public float GetSeasonProduction(SeasonId season)
        {
            int i = (int)season;
            if (SeasonalProduction == null || i < 0 || i >= SeasonalProduction.Length)
            {
                return 1f;
            }

            return SeasonalProduction[i];
        }
    }
}
