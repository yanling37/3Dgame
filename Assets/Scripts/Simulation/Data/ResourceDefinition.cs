using System;
using UnityEngine;

namespace DivineWorld.Simulation.Data
{
    /// <summary>
    /// Data-driven resource type. New resources should be registered in ResourceCatalog,
    /// not by copying ResourceSystem logic.
    /// </summary>
    [Serializable]
    public class ResourceDefinition
    {
        public ResourceId Id;
        public string DisplayName;
        public bool CanSpoil;
        [Tooltip("Daily fraction of stock lost when CanSpoil.")]
        public float BaseSpoilRate;
        [Tooltip("Daily per-capita consumption. 0 = not consumed by population.")]
        public float BaseConsumePerCapita;
        [Tooltip("Base daily production scale used by ResourceSystem.")]
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

    public static class ResourceCatalog
    {
        static ResourceDefinition[] _all;

        public static ResourceDefinition[] All
        {
            get
            {
                if (_all == null)
                {
                    _all = CreateDefault();
                }

                return _all;
            }
        }

        public static ResourceDefinition Get(ResourceId id)
        {
            var all = All;
            int i = (int)id;
            if (i < 0 || i >= all.Length)
            {
                return all[0];
            }

            return all[i];
        }

        static ResourceDefinition[] CreateDefault()
        {
            // Index must match ResourceId.
            return new[]
            {
                new ResourceDefinition
                {
                    Id = ResourceId.Food,
                    DisplayName = "粮食",
                    CanSpoil = true,
                    BaseSpoilRate = 0.008f,
                    BaseConsumePerCapita = 0.02f,
                    BaseProductionScale = 0.02f,
                    UsesLabor = true,
                    UsesTech = true,
                    UsesWeather = true,
                    UsesHarvestBlessing = true,
                    AffectsCarryingCapacity = true,
                    SeasonalProduction = new[] { 1.15f, 1.25f, 1.05f, 0.55f }
                },
                new ResourceDefinition
                {
                    Id = ResourceId.Water,
                    DisplayName = "水",
                    CanSpoil = false,
                    BaseConsumePerCapita = 0.015f,
                    BaseProductionScale = 0.015f,
                    UsesLabor = false,
                    UsesTech = false,
                    UsesWeather = true,
                    AffectsCarryingCapacity = true,
                    SeasonalProduction = new[] { 1.2f, 0.85f, 1.0f, 0.7f }
                },
                new ResourceDefinition
                {
                    Id = ResourceId.Timber,
                    DisplayName = "木材",
                    CanSpoil = false,
                    BaseProductionScale = 0.01f,
                    UsesLabor = true,
                    UsesTech = true,
                    UsesWeather = false,
                    SeaSuppressed = true,
                    SeaFallbackYield = 0.2f,
                    SeasonalProduction = new[] { 1.05f, 1.0f, 1.15f, 0.8f }
                },
                new ResourceDefinition
                {
                    Id = ResourceId.Ore,
                    DisplayName = "矿石",
                    CanSpoil = false,
                    BaseProductionScale = 0.008f,
                    UsesLabor = true,
                    UsesTech = true,
                    UsesWeather = false,
                    SeaSuppressed = true,
                    SeaFallbackYield = 0.3f,
                    SeasonalProduction = new[] { 1f, 1f, 1f, 0.9f }
                },
                new ResourceDefinition
                {
                    Id = ResourceId.Faith,
                    DisplayName = "信仰资源",
                    CanSpoil = false,
                    BaseProductionScale = 0.0004f,
                    UsesLabor = false,
                    UsesTech = false,
                    UsesWeather = false,
                    SeasonalProduction = new[] { 1.05f, 1f, 1.1f, 1.15f }
                },
                new ResourceDefinition
                {
                    Id = ResourceId.Knowledge,
                    DisplayName = "知识",
                    CanSpoil = false,
                    BaseProductionScale = 0.00025f,
                    UsesLabor = false,
                    UsesTech = false,
                    UsesWeather = false,
                    SeasonalProduction = new[] { 1.05f, 0.95f, 1.1f, 1.2f }
                },
                new ResourceDefinition
                {
                    Id = ResourceId.Magic,
                    DisplayName = "Mana",
                    CanSpoil = false,
                    BaseProductionScale = 0.0001f,
                    UsesLabor = false,
                    UsesTech = false,
                    UsesWeather = false,
                    SeasonalProduction = new[] { 1.1f, 1.2f, 1.0f, 0.9f }
                }
            };
        }
    }
}
