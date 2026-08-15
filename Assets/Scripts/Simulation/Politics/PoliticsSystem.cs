using System.Collections.Generic;
using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Politics
{
    /// <summary>
    /// P2-C v0.1 politics helpers. Not invoked by the daily pipeline or FastForward.
    /// Relation values change only through <see cref="AdjustRelation"/> (debug / future diplomacy) or reset.
    /// Does not read or write population, resources, disease, or stability.
    /// </summary>
    public static class PoliticsSystem
    {
        public static readonly RegionId[] Regions =
        {
            RegionId.Theocracy,
            RegionId.Empire,
            RegionId.Sea
        };

        public static readonly (RegionId A, RegionId B)[] StandardPairs =
        {
            (RegionId.Theocracy, RegionId.Empire),
            (RegionId.Theocracy, RegionId.Sea),
            (RegionId.Empire, RegionId.Sea)
        };

        public static void Canonical(RegionId a, RegionId b, out RegionId source, out RegionId target)
        {
            if ((int)a <= (int)b)
            {
                source = a;
                target = b;
                return;
            }

            source = b;
            target = a;
        }

        public static PoliticsState CreateInitialState(PoliticalConfig config = null)
        {
            var cfg = config != null ? config.Clone() : PoliticalConfig.CreateDefault();
            var state = new PoliticsState
            {
                Config = cfg,
                Relations = new List<PoliticalRelation>(StandardPairs.Length)
            };

            for (int i = 0; i < StandardPairs.Length; i++)
            {
                var pair = StandardPairs[i];
                Canonical(pair.A, pair.B, out RegionId source, out RegionId target);
                float value = LookupInitialValue(cfg, source, target);
                value = ClampValue(value, cfg);
                state.Relations.Add(new PoliticalRelation
                {
                    SourceRegionId = source,
                    TargetRegionId = target,
                    RelationValue = value,
                    RelationState = ResolveState(value, cfg),
                    LastChangedDay = 0,
                    History = new List<PoliticalHistoryEntry>()
                });
            }

            return state;
        }

        public static void EnsureInitialized(WorldState world)
        {
            if (world == null)
            {
                return;
            }

            if (world.Politics == null || world.Politics.Relations == null || world.Politics.Relations.Count == 0)
            {
                world.Politics = CreateInitialState();
            }
        }

        public static void Reset(WorldState world, PoliticalConfig config = null)
        {
            if (world == null)
            {
                return;
            }

            world.Politics = CreateInitialState(config ?? world.Politics?.Config);
        }

        public static PoliticalRelationState ResolveState(float value, PoliticalConfig config)
        {
            var cfg = config ?? PoliticalConfig.CreateDefault();
            if (!IsFinite(value))
            {
                return PoliticalRelationState.Neutral;
            }

            // War is reserved and is never returned in v0.1.
            if (value >= cfg.FriendlyMin)
            {
                return PoliticalRelationState.Friendly;
            }

            if (value <= cfg.HostileMax)
            {
                return PoliticalRelationState.Hostile;
            }

            if (value <= cfg.TenseMax)
            {
                return PoliticalRelationState.Tense;
            }

            return PoliticalRelationState.Neutral;
        }

        public static bool AdjustRelation(
            WorldState world,
            RegionId a,
            RegionId b,
            float delta,
            string reason)
        {
            if (world == null || a == b || !IsFinite(delta))
            {
                return false;
            }

            EnsureInitialized(world);
            var relation = world.Politics.FindRelation(a, b);
            if (relation == null)
            {
                return false;
            }

            var cfg = world.Politics.Config ?? PoliticalConfig.CreateDefault();
            float oldValue = relation.RelationValue;
            float newValue = ClampValue(oldValue + delta, cfg);
            if (Mathf.Approximately(oldValue, newValue))
            {
                relation.RelationState = ResolveState(relation.RelationValue, cfg);
                return false;
            }

            int day = world.TotalDays;
            relation.History = relation.History ?? new List<PoliticalHistoryEntry>();
            relation.History.Add(new PoliticalHistoryEntry
            {
                Day = day,
                OldValue = oldValue,
                NewValue = newValue,
                Reason = string.IsNullOrEmpty(reason) ? "Unspecified" : reason,
                SourceRegionId = relation.SourceRegionId,
                TargetRegionId = relation.TargetRegionId
            });

            relation.RelationValue = newValue;
            relation.RelationState = ResolveState(newValue, cfg);
            relation.LastChangedDay = day;
            return true;
        }

        public static bool DebugAdjust(WorldState world, RegionId a, RegionId b, float signedStep)
        {
            string sign = signedStep > 0f ? "+" : "";
            string reason = "Debug Adjustment " + sign + signedStep.ToString("0.##");
            return AdjustRelation(world, a, b, signedStep, reason);
        }

        public static float ClampValue(float value, PoliticalConfig config)
        {
            var cfg = config ?? PoliticalConfig.CreateDefault();
            if (!IsFinite(value))
            {
                return 0f;
            }

            return Mathf.Clamp(value, cfg.MinRelationValue, cfg.MaxRelationValue);
        }

        public static bool IsFinite(float value)
        {
            return !(float.IsNaN(value) || float.IsInfinity(value));
        }

        static float LookupInitialValue(PoliticalConfig config, RegionId source, RegionId target)
        {
            if (config == null || config.InitialPoliticalRelations == null)
            {
                return 0f;
            }

            for (int i = 0; i < config.InitialPoliticalRelations.Length; i++)
            {
                var entry = config.InitialPoliticalRelations[i];
                if (entry == null)
                {
                    continue;
                }

                Canonical(entry.RegionA, entry.RegionB, out RegionId a, out RegionId b);
                if (a == source && b == target)
                {
                    return entry.RelationValue;
                }
            }

            return 0f;
        }
    }
}
