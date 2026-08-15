using System;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    public enum ResourceNodeType
    {
        Food = 0,
        Water = 1,
        Wood = 2,
        Mineral = 3,
        Magic = 4
    }

    /// <summary>
    /// Observation-layer resource marker. Amount/Capacity are copied from
    /// ObservationSnapshot; this is not a second resource inventory.
    /// </summary>
    public sealed class ResourceNode
    {
        public ResourceNode(
            string nodeId,
            ResourceNodeType type,
            ResourceId resourceId,
            RegionId regionId,
            float amount,
            float capacity,
            bool active,
            float localX,
            float localY,
            float localZ)
        {
            NodeId = nodeId ?? string.Empty;
            Type = type;
            ResourceId = resourceId;
            RegionId = regionId;
            Amount = amount;
            Capacity = capacity;
            Active = active;
            LocalX = localX;
            LocalY = localY;
            LocalZ = localZ;
        }

        public string NodeId { get; }
        public ResourceNodeType Type { get; }
        public ResourceId ResourceId { get; }
        public RegionId RegionId { get; }
        public float Amount { get; }
        public float Capacity { get; }
        public bool Active { get; }
        public float LocalX { get; }
        public float LocalY { get; }
        public float LocalZ { get; }

        public float Fill
        {
            get
            {
                if (Capacity <= 0.0001f)
                {
                    return Active ? 1f : 0f;
                }

                float t = Amount / Capacity;
                if (t < 0f) return 0f;
                if (t > 1f) return 1f;
                return t;
            }
        }
    }

    /// <summary>
    /// Fixed placeholder layout per resource type. Visuals may later swap per civilization.
    /// </summary>
    public static class ResourceNodeLayout
    {
        public static readonly ResourceNodeType[] Types =
        {
            ResourceNodeType.Food,
            ResourceNodeType.Water,
            ResourceNodeType.Wood,
            ResourceNodeType.Mineral,
            ResourceNodeType.Magic
        };

        public const int NodesPerRegion = 5;
        public const float RegionSpacing = 7f;

        public static ResourceId ResourceIdOf(ResourceNodeType type)
        {
            switch (type)
            {
                case ResourceNodeType.Food: return ResourceId.Food;
                case ResourceNodeType.Water: return ResourceId.Water;
                case ResourceNodeType.Wood: return ResourceId.Timber;
                case ResourceNodeType.Mineral: return ResourceId.Ore;
                default: return ResourceId.Magic;
            }
        }

        public static string DisplayName(ResourceNodeType type)
        {
            switch (type)
            {
                case ResourceNodeType.Food: return "Farm";
                case ResourceNodeType.Water: return "Spring";
                case ResourceNodeType.Wood: return "Forest";
                case ResourceNodeType.Mineral: return "Mine";
                default: return "Mana";
            }
        }

        public static void LocalOffset(ResourceNodeType type, out float x, out float y, out float z)
        {
            switch (type)
            {
                case ResourceNodeType.Food: x = -1.7f; y = 0.25f; z = 1.4f; return;
                case ResourceNodeType.Water: x = 1.7f; y = 0.25f; z = 1.4f; return;
                case ResourceNodeType.Wood: x = -1.7f; y = 0.35f; z = -1.4f; return;
                case ResourceNodeType.Mineral: x = 1.7f; y = 0.25f; z = -1.4f; return;
                default: x = 0f; y = 1.6f; z = 1.8f; return;
            }
        }

        public static float RegionRootX(RegionId regionId)
        {
            return ((int)regionId - 1) * RegionSpacing;
        }

        public static float ReadAmount(RegionObservationSnapshot snap, ResourceNodeType type)
        {
            if (snap == null)
            {
                return 0f;
            }

            switch (type)
            {
                case ResourceNodeType.Food: return snap.Food;
                case ResourceNodeType.Water: return snap.Water;
                case ResourceNodeType.Wood: return snap.Wood;
                case ResourceNodeType.Mineral: return snap.Mineral;
                default: return snap.Magic;
            }
        }

        public static float ReadCapacity(RegionObservationSnapshot snap, ResourceNodeType type)
        {
            if (snap == null)
            {
                return 0f;
            }

            return type == ResourceNodeType.Water ? snap.WaterCapacity : 0f;
        }
    }

    /// <summary>
    /// Pooled observation resource nodes (3 regions × 5 types). Apply() updates values
    /// from a snapshot; it does not spawn extra nodes over time.
    /// </summary>
    public sealed class ResourceNodeState
    {
        ResourceNode[] _nodes = Array.Empty<ResourceNode>();

        public int Count => _nodes.Length;
        public ResourceNode[] Nodes => _nodes;

        public ResourceNode Find(RegionId regionId, ResourceNodeType type)
        {
            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_nodes[i].RegionId == regionId && _nodes[i].Type == type)
                {
                    return _nodes[i];
                }
            }

            return null;
        }

        public int CountFor(RegionId regionId)
        {
            int n = 0;
            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_nodes[i].RegionId == regionId)
                {
                    n++;
                }
            }

            return n;
        }

        public void Clear()
        {
            Apply(WorldObservationSnapshot.Empty);
        }

        public void Apply(WorldObservationSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Regions == null || snapshot.Regions.Length == 0)
            {
                _nodes = Array.Empty<ResourceNode>();
                return;
            }

            int needed = snapshot.Regions.Length * ResourceNodeLayout.NodesPerRegion;
            if (_nodes.Length != needed)
            {
                _nodes = new ResourceNode[needed];
            }

            int w = 0;
            for (int r = 0; r < snapshot.Regions.Length; r++)
            {
                var region = snapshot.Regions[r];
                if (region == null)
                {
                    continue;
                }

                for (int t = 0; t < ResourceNodeLayout.Types.Length; t++)
                {
                    var type = ResourceNodeLayout.Types[t];
                    float amount = ResourceNodeLayout.ReadAmount(region, type);
                    float capacity = ResourceNodeLayout.ReadCapacity(region, type);
                    ResourceNodeLayout.LocalOffset(type, out float x, out float y, out float z);
                    bool active = amount > 0.0001f && !float.IsNaN(amount) && !float.IsInfinity(amount);
                    _nodes[w++] = new ResourceNode(
                        region.RegionId + "_" + type,
                        type,
                        ResourceNodeLayout.ResourceIdOf(type),
                        region.RegionId,
                        amount,
                        capacity,
                        active,
                        x,
                        y,
                        z);
                }
            }

            if (w < _nodes.Length)
            {
                var trimmed = new ResourceNode[w];
                Array.Copy(_nodes, trimmed, w);
                _nodes = trimmed;
            }
        }
    }
}
