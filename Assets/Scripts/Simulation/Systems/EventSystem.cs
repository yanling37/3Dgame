using System.Collections.Generic;
using System.Text;
using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Formal region events with start/duration/expiry. Regional events are independent per region.
    /// </summary>
    public static class EventSystem
    {
        public static void EvaluateAndApply(WorldState world, SimulationConfig config)
        {
            if (world?.Regions == null || config == null)
            {
                return;
            }

            foreach (var region in world.Regions)
            {
                ExpireFinished(region, world.TotalDays);

                MaybeUpsert(
                    region,
                    world,
                    SimEventType.FoodShortage,
                    region.LastFoodReserveDays < config.FoodShortageReserveDays
                    || region.Get(ResourceId.Food) < region.Population * config.FoodShortageRatio,
                    config.FoodShortageEventDuration,
                    1f);

                MaybeUpsert(
                    region,
                    world,
                    SimEventType.DiseaseOutbreak,
                    region.DiseasePressure >= 0.25f,
                    config.DiseaseOutbreakEventDuration,
                    region.DiseasePressure);

                MaybeUpsert(region, world, SimEventType.LowStability, region.Stability < 0.45f, 25, 1f - region.Stability);
                MaybeUpsert(
                    region,
                    world,
                    SimEventType.HighStability,
                    region.Stability >= 1.1f && region.Get(ResourceId.Food) > region.Population * config.FoodSurplusRatio,
                    20,
                    region.Stability);

                // Deterministic regional disaster roll — same predicate used by FastForward forecast.
                float roll = Hash01(world.RandomSeed, world.TotalDays, (int)region.Id, 3);
                if (roll < config.NaturalDisasterChancePerDay)
                {
                    float severity = 0.8f + Hash01(world.RandomSeed, world.TotalDays, (int)region.Id, 7) * 0.4f;
                    Upsert(region, world, SimEventType.NaturalDisaster, config.NaturalDisasterDuration, severity, SimEventScope.Regional);
                    ApplyEventImpact(region, FindActive(region, SimEventType.NaturalDisaster, world.TotalDays), config, initialImpact: true);
                }

                region.LastEvent = Summarize(region);
            }
        }

        public static void ApplyYearTurn(WorldState world)
        {
            if (world?.Regions == null)
            {
                return;
            }

            foreach (var region in world.Regions)
            {
                Upsert(region, world, SimEventType.YearTurn, 5, 1f, SimEventScope.Global);
                region.LastEvent = $"新年纪事 · {world.Year}";
            }
        }

        public static void ApplyEventImpact(RegionState region, RegionEvent evt, SimulationConfig config, bool initialImpact)
        {
            if (region == null || evt == null)
            {
                return;
            }

            switch (evt.EventType)
            {
                case SimEventType.DiseaseOutbreak:
                    if (initialImpact)
                    {
                        region.DiseasePressure = Mathf.Clamp01(region.DiseasePressure + 0.15f * evt.Severity);
                        region.Stability = Mathf.Max(0.05f, region.Stability - 0.05f * evt.Severity);
                    }
                    break;
                case SimEventType.FoodShortage:
                    if (initialImpact)
                    {
                        region.Stability = Mathf.Max(0.05f, region.Stability - 0.04f * evt.Severity);
                    }
                    break;
                case SimEventType.NaturalDisaster:
                    if (initialImpact)
                    {
                        region.Stability = Mathf.Max(0.05f, region.Stability - 0.1f * evt.Severity);
                        region.Add(ResourceId.Food, -region.Population * 0.08f * evt.Severity);
                    }
                    break;
                case SimEventType.LowStability:
                    if (initialImpact)
                    {
                        region.Stability = Mathf.Max(0.05f, region.Stability - 0.02f);
                    }
                    break;
            }
        }

        /// <summary>
        /// Active NaturalDisaster reduces food production continuously until expiry.
        /// </summary>
        public static float GetFoodProductionEventModifier(RegionState region, int totalDay, SimulationConfig config)
        {
            float severity = region.GetActiveEventSeverity(SimEventType.NaturalDisaster, totalDay);
            if (severity <= 0f)
            {
                return 1f;
            }

            float penalty = config.NaturalDisasterFoodProductionPenalty * Mathf.Clamp01(severity);
            return Mathf.Clamp(1f - penalty, 0f, 1f);
        }

        /// <summary>
        /// Forecast regional disaster breakpoints using the same Hash01 predicate as daily evaluation.
        /// </summary>
        public static List<RegionEvent> ForecastBreakpoints(WorldState world, int fromDayExclusive, int toDayExclusive, SimulationConfig config)
        {
            var list = new List<RegionEvent>();
            if (world?.Regions == null || config == null)
            {
                return list;
            }

            for (int day = fromDayExclusive + 1; day <= toDayExclusive; day++)
            {
                foreach (var region in world.Regions)
                {
                    float roll = Hash01(world.RandomSeed, day, (int)region.Id, 3);
                    if (roll < config.NaturalDisasterChancePerDay)
                    {
                        float severity = 0.8f + Hash01(world.RandomSeed, day, (int)region.Id, 7) * 0.4f;
                        list.Add(new RegionEvent
                        {
                            EventId = $"fc_disaster_{region.Id}_{day}",
                            EventType = SimEventType.NaturalDisaster,
                            RegionId = region.Id,
                            Scope = SimEventScope.Regional,
                            StartDay = day,
                            Duration = config.NaturalDisasterDuration,
                            Severity = severity
                        });
                    }
                }
            }

            list.Sort((a, b) => a.StartDay.CompareTo(b.StartDay));
            return list;
        }

        static void MaybeUpsert(RegionState region, WorldState world, SimEventType type, bool condition, int duration, float severity)
        {
            if (!condition)
            {
                return;
            }

            Upsert(region, world, type, duration, severity, SimEventScope.Regional);
        }

        static void Upsert(RegionState region, WorldState world, SimEventType type, int duration, float severity, SimEventScope scope)
        {
            if (region.ActiveEvents == null)
            {
                region.ActiveEvents = new List<RegionEvent>();
            }

            for (int i = 0; i < region.ActiveEvents.Count; i++)
            {
                if (region.ActiveEvents[i].EventType == type && region.ActiveEvents[i].IsActiveOn(world.TotalDays))
                {
                    region.ActiveEvents[i].Severity = Mathf.Max(region.ActiveEvents[i].Severity, severity);
                    region.ActiveEvents[i].Duration = Mathf.Max(region.ActiveEvents[i].Duration, duration);
                    return;
                }
            }

            region.ActiveEvents.Add(new RegionEvent
            {
                EventId = $"{type}_{region.Id}_{world.TotalDays}",
                EventType = type,
                RegionId = region.Id,
                Scope = scope,
                StartDay = world.TotalDays,
                Duration = duration,
                Severity = severity
            });
        }

        static RegionEvent FindActive(RegionState region, SimEventType type, int totalDay)
        {
            if (region.ActiveEvents == null)
            {
                return null;
            }

            for (int i = 0; i < region.ActiveEvents.Count; i++)
            {
                if (region.ActiveEvents[i].EventType == type && region.ActiveEvents[i].IsActiveOn(totalDay))
                {
                    return region.ActiveEvents[i];
                }
            }

            return null;
        }

        static void ExpireFinished(RegionState region, int totalDay)
        {
            if (region.ActiveEvents == null)
            {
                return;
            }

            region.ActiveEvents.RemoveAll(e => !e.IsActiveOn(totalDay));
        }

        static string Summarize(RegionState region)
        {
            if (region.ActiveEvents == null || region.ActiveEvents.Count == 0)
            {
                return "平静";
            }

            var sb = new StringBuilder();
            for (int i = 0; i < region.ActiveEvents.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                var e = region.ActiveEvents[i];
                sb.Append(Label(e.EventType));
                if (e.Scope == SimEventScope.Global)
                {
                    sb.Append("[G]");
                }
            }

            return sb.ToString();
        }

        public static string Label(SimEventType type)
        {
            switch (type)
            {
                case SimEventType.FoodShortage: return "粮食短缺";
                case SimEventType.DiseaseOutbreak: return "疫病爆发";
                case SimEventType.LowStability: return "低稳定";
                case SimEventType.HighStability: return "高稳定";
                case SimEventType.YearTurn: return "新年";
                case SimEventType.NaturalDisaster: return "天灾";
                default: return type.ToString();
            }
        }

        public static float Hash01(int seed, int day, int region, int salt)
        {
            unchecked
            {
                int h = seed;
                h = h * 73856093 ^ day * 19349663;
                h = h * 83492791 ^ region * 50331653;
                h = (int)(h * 2654435761u) ^ salt;
                h &= 0x7fffffff;
                return (h % 10000) / 10000f;
            }
        }
    }
}
