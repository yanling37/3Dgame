using System;
using UnityEngine;

namespace DivineWorld.Simulation.Data
{
    /// <summary>
    /// Static definition of a resource type (data-driven; add new resources without rewriting systems).
    /// </summary>
    [Serializable]
    public class ResourceTypeDefinition
    {
        public ResourceId Id;
        public string DisplayName;
        public ResourceLifecycle Lifecycle;
        public float BaseSpoilageRate;
        public bool CanSpoil => Lifecycle == ResourceLifecycle.Perishable;
        public bool HasStorageCapacity => Lifecycle == ResourceLifecycle.CapacityLimited;
    }

    /// <summary>
    /// Runtime stock + last-tick diagnostics for one resource in a region.
    /// Production capacity is stored separately on RegionState (not derived from stock).
    /// </summary>
    [Serializable]
    public class ResourceState
    {
        public ResourceId Id;
        public float Stock;
        public float LastProduction;
        public float LastConsumption;
        public float LastSpoilage;
        public float LastCapacity;
    }

    /// <summary>
    /// Shared catalog of resource type rules for P2-A.
    /// </summary>
    public static class ResourceCatalog
    {
        public static readonly ResourceTypeDefinition[] All =
        {
            new ResourceTypeDefinition
            {
                Id = ResourceId.Food,
                DisplayName = "粮食",
                Lifecycle = ResourceLifecycle.Perishable,
                BaseSpoilageRate = 0.006f
            },
            new ResourceTypeDefinition
            {
                Id = ResourceId.Water,
                DisplayName = "水",
                Lifecycle = ResourceLifecycle.CapacityLimited,
                BaseSpoilageRate = 0f
            },
            new ResourceTypeDefinition
            {
                Id = ResourceId.Timber,
                DisplayName = "木材",
                Lifecycle = ResourceLifecycle.Persistent,
                BaseSpoilageRate = 0f
            },
            new ResourceTypeDefinition
            {
                Id = ResourceId.Ore,
                DisplayName = "矿石",
                Lifecycle = ResourceLifecycle.Persistent,
                BaseSpoilageRate = 0f
            },
            new ResourceTypeDefinition
            {
                Id = ResourceId.Faith,
                DisplayName = "信仰资源",
                Lifecycle = ResourceLifecycle.Persistent,
                BaseSpoilageRate = 0f
            },
            new ResourceTypeDefinition
            {
                Id = ResourceId.Knowledge,
                DisplayName = "知识",
                Lifecycle = ResourceLifecycle.Persistent,
                BaseSpoilageRate = 0f
            },
            new ResourceTypeDefinition
            {
                Id = ResourceId.Magic,
                DisplayName = "魔力",
                Lifecycle = ResourceLifecycle.Persistent,
                BaseSpoilageRate = 0f
            }
        };

        public static ResourceTypeDefinition Get(ResourceId id)
        {
            int index = (int)id;
            if (index < 0 || index >= All.Length)
            {
                return All[0];
            }

            return All[index];
        }
    }

    /// <summary>
    /// Applies lifecycle rules after production/consumption deltas are known.
    /// </summary>
    public static class ResourceRules
    {
        public static float Apply(
            ResourceTypeDefinition type,
            float currentStock,
            float production,
            float consumption,
            float spoilageRate,
            float capacity,
            out float spoilageApplied)
        {
            spoilageApplied = 0f;
            if (float.IsNaN(currentStock) || float.IsInfinity(currentStock))
            {
                currentStock = 0f;
            }

            production = SanitizeNonNegative(production);
            consumption = SanitizeNonNegative(consumption);
            spoilageRate = Mathf.Clamp(spoilageRate, 0f, 1f);

            float next = currentStock + production - consumption;

            if (type.Lifecycle == ResourceLifecycle.Perishable)
            {
                // Spoilage from remaining stock after production/consumption netting.
                float spoilBase = Mathf.Max(0f, next);
                spoilageApplied = spoilBase * spoilageRate;
                next -= spoilageApplied;
            }

            if (type.Lifecycle == ResourceLifecycle.CapacityLimited)
            {
                float cap = Mathf.Max(0f, capacity);
                next = Mathf.Min(next, cap);
            }

            if (float.IsNaN(next) || float.IsInfinity(next))
            {
                next = 0f;
            }

            return Mathf.Max(0f, next);
        }

        static float SanitizeNonNegative(float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                return 0f;
            }

            return value;
        }
    }
}
