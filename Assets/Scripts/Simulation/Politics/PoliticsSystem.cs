using System;
using System.Collections.Generic;
using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Politics
{
    /// <summary>
    /// P2-C v0.2 diplomatic actions. Not invoked by the daily pipeline or FastForward.
    /// Data flow: DiplomaticAction → ApplyDiplomaticAction → PoliticalRelation.RelationValue
    /// → ResolveState → RelationState → PoliticalHistory / DiplomaticHistory.
    /// Relation values never drift on their own. Population and resources are not read or written.
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
                Relations = new List<PoliticalRelation>(StandardPairs.Length),
                DiplomaticHistory = new List<PoliticalHistoryEntry>(),
                Treaties = new List<Treaty>()
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

            if (world.Politics.DiplomaticHistory == null)
            {
                world.Politics.DiplomaticHistory = new List<PoliticalHistoryEntry>();
            }

            if (world.Politics.Treaties == null)
            {
                world.Politics.Treaties = new List<Treaty>();
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

            // War is reserved and is never returned in v0.2.
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

        /// <summary>
        /// Single relation-mutation entry. Every Improve / Worsen / Incident / debug nudge goes through here.
        /// Does not assign RelationState from ActionType; state is always derived from the clamped value.
        /// </summary>
        public static bool ApplyDiplomaticAction(WorldState world, DiplomaticAction action)
        {
            if (world == null || action == null || action.SourceRegion == action.TargetRegion || !IsFinite(action.Delta))
            {
                return false;
            }

            EnsureInitialized(world);
            var relation = world.Politics.FindRelation(action.SourceRegion, action.TargetRegion);
            if (relation == null)
            {
                return false;
            }

            var cfg = world.Politics.Config ?? PoliticalConfig.CreateDefault();
            float oldValue = relation.RelationValue;
            float newValue = ClampValue(oldValue + action.Delta, cfg);
            int day = world.TotalDays;
            action.Day = day;
            if (string.IsNullOrEmpty(action.Reason))
            {
                action.Reason = DefaultReason(action.ActionType);
            }

            if (Mathf.Approximately(oldValue, newValue))
            {
                relation.RelationState = ResolveState(relation.RelationValue, cfg);
                return false;
            }

            float appliedDelta = newValue - oldValue;
            AppendHistory(world, relation, new PoliticalHistoryEntry
            {
                Day = day,
                SourceRegionId = action.SourceRegion,
                TargetRegionId = action.TargetRegion,
                ActionType = action.ActionType,
                OldValue = oldValue,
                Delta = appliedDelta,
                NewValue = newValue,
                Reason = action.Reason
            });

            relation.RelationValue = newValue;
            relation.RelationState = ResolveState(newValue, cfg);
            relation.LastChangedDay = day;
            return true;
        }

        public static bool ImproveRelations(
            WorldState world,
            RegionId source,
            RegionId target,
            float delta,
            string reason)
        {
            if (!IsFinite(delta))
            {
                return false;
            }

            return ApplyDiplomaticAction(world, DiplomaticAction.Create(
                source,
                target,
                DiplomaticActionType.ImproveRelations,
                Mathf.Abs(delta),
                string.IsNullOrEmpty(reason) ? DiplomaticAction.DefaultImproveReason : reason));
        }

        public static bool WorsenRelations(
            WorldState world,
            RegionId source,
            RegionId target,
            float delta,
            string reason)
        {
            if (!IsFinite(delta))
            {
                return false;
            }

            return ApplyDiplomaticAction(world, DiplomaticAction.Create(
                source,
                target,
                DiplomaticActionType.WorsenRelations,
                -Mathf.Abs(delta),
                string.IsNullOrEmpty(reason) ? DiplomaticAction.DefaultWorsenReason : reason));
        }

        public static bool ApplyDiplomaticIncident(WorldState world, DiplomaticIncident incident)
        {
            if (incident == null)
            {
                return false;
            }

            var action = DiplomaticAction.Create(
                incident.SourceRegion,
                incident.TargetRegion,
                DiplomaticActionType.DiplomaticIncident,
                incident.Delta,
                string.IsNullOrEmpty(incident.Reason) ? DiplomaticAction.DefaultIncidentReason : incident.Reason);
            bool applied = ApplyDiplomaticAction(world, action);
            incident.Day = action.Day;
            return applied;
        }

        /// <summary>
        /// Treaty placeholder. Does not change RelationValue, population, or resources.
        /// Trade / Alliance kinds store data only — no trade flow, no military effect.
        /// </summary>
        public static Treaty CreateTreaty(
            WorldState world,
            TreatyType type,
            RegionId source,
            RegionId target,
            int durationDays,
            string reason)
        {
            if (world == null || source == target)
            {
                return null;
            }

            EnsureInitialized(world);
            var relation = world.Politics.FindRelation(source, target);
            if (relation == null)
            {
                return null;
            }

            int day = world.TotalDays;
            int endDay = durationDays < 0 ? -1 : day + durationDays;
            var treaty = new Treaty
            {
                TreatyType = type,
                SourceRegion = source,
                TargetRegion = target,
                StartDay = day,
                EndDay = endDay,
                Active = true
            };
            world.Politics.Treaties.Add(treaty);

            float value = relation.RelationValue;
            AppendHistory(world, relation, new PoliticalHistoryEntry
            {
                Day = day,
                SourceRegionId = source,
                TargetRegionId = target,
                ActionType = DiplomaticActionType.Treaty,
                OldValue = value,
                Delta = 0f,
                NewValue = value,
                Reason = string.IsNullOrEmpty(reason) ? type + " Treaty" : reason
            });
            return treaty;
        }

        public static bool ExpireTreaty(WorldState world, Treaty treaty, string reason)
        {
            if (world == null || treaty == null || !treaty.Active)
            {
                return false;
            }

            EnsureInitialized(world);
            treaty.Active = false;
            var relation = world.Politics.FindRelation(treaty.SourceRegion, treaty.TargetRegion);
            if (relation != null)
            {
                float value = relation.RelationValue;
                AppendHistory(world, relation, new PoliticalHistoryEntry
                {
                    Day = world.TotalDays,
                    SourceRegionId = treaty.SourceRegion,
                    TargetRegionId = treaty.TargetRegion,
                    ActionType = DiplomaticActionType.Treaty,
                    OldValue = value,
                    Delta = 0f,
                    NewValue = value,
                    Reason = string.IsNullOrEmpty(reason) ? "Treaty Expired" : reason
                });
            }

            return true;
        }

        public static IReadOnlyList<Treaty> GetActiveTreaties(WorldState world)
        {
            if (world == null || world.Politics == null || world.Politics.Treaties == null)
            {
                return Array.Empty<Treaty>();
            }

            int day = world.TotalDays;
            var active = new List<Treaty>();
            for (int i = 0; i < world.Politics.Treaties.Count; i++)
            {
                var treaty = world.Politics.Treaties[i];
                if (treaty != null && treaty.IsActiveAt(day))
                {
                    active.Add(treaty);
                }
            }

            return active;
        }

        public static bool AdjustRelation(
            WorldState world,
            RegionId a,
            RegionId b,
            float delta,
            string reason)
        {
            return ApplyDiplomaticAction(world, DiplomaticAction.Create(
                a,
                b,
                InferActionType(delta, reason),
                delta,
                reason));
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

        static DiplomaticActionType InferActionType(float delta, string reason)
        {
            if (!string.IsNullOrEmpty(reason))
            {
                if (reason.IndexOf("Incident", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return DiplomaticActionType.DiplomaticIncident;
                }

                if (reason.IndexOf("Treaty", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return DiplomaticActionType.Treaty;
                }
            }

            return delta < 0f ? DiplomaticActionType.WorsenRelations : DiplomaticActionType.ImproveRelations;
        }

        static string DefaultReason(DiplomaticActionType type)
        {
            switch (type)
            {
                case DiplomaticActionType.WorsenRelations:
                    return DiplomaticAction.DefaultWorsenReason;
                case DiplomaticActionType.DiplomaticIncident:
                    return DiplomaticAction.DefaultIncidentReason;
                case DiplomaticActionType.Treaty:
                    return "Treaty";
                default:
                    return DiplomaticAction.DefaultImproveReason;
            }
        }

        static void AppendHistory(WorldState world, PoliticalRelation relation, PoliticalHistoryEntry entry)
        {
            relation.History = relation.History ?? new List<PoliticalHistoryEntry>();
            relation.History.Add(entry);
            world.Politics.DiplomaticHistory = world.Politics.DiplomaticHistory ?? new List<PoliticalHistoryEntry>();
            world.Politics.DiplomaticHistory.Add(entry);
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
