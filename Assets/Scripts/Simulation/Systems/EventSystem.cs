using System.Collections.Generic;
using System.Text;
using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Formal region events enter simulation state (not just HUD strings).
    /// </summary>
    public static class EventSystem
    {
        public static void EvaluateAndApply(WorldState world, System.Random rng)
        {
            foreach (var region in world.Regions)
            {
                ExpireFinished(region, world.TotalDays);
                MaybeUpsert(region, world, SimEventType.FoodShortage, region.Get(ResourceId.Food) < region.Population * 0.2f, 20, 1f);
                MaybeUpsert(region, world, SimEventType.DiseaseOutbreak, region.DiseasePressure >= 0.25f, 30, region.DiseasePressure);
                MaybeUpsert(region, world, SimEventType.LowStability, region.Stability < 0.45f, 25, 1f - region.Stability);
                MaybeUpsert(region, world, SimEventType.HighStability, region.Stability >= 1.1f && region.Get(ResourceId.Food) > region.Population * 0.8f, 20, region.Stability);

                // Rare natural disaster roll (reproducible via seed).
                if (rng.NextDouble() < 0.002)
                {
                    Upsert(region, world, SimEventType.NaturalDisaster, 15, 0.8f + (float)rng.NextDouble() * 0.4f);
                    region.Stability = Mathf.Max(0.1f, region.Stability - 0.08f);
                    region.Add(ResourceId.Food, -region.Population * 0.05f);
                }

                region.LastEvent = Summarize(region);
            }
        }

        public static void ApplyYearTurn(WorldState world)
        {
            foreach (var region in world.Regions)
            {
                Upsert(region, world, SimEventType.YearTurn, 5, 1f);
                region.LastEvent = $"新年纪事 · {world.Year}";
            }
        }

        public static void ApplyEventImpact(RegionState region, RegionEvent evt)
        {
            switch (evt.EventType)
            {
                case SimEventType.DiseaseOutbreak:
                    region.DiseasePressure = Mathf.Clamp01(region.DiseasePressure + 0.15f * evt.Severity);
                    region.Stability = Mathf.Max(0.1f, region.Stability - 0.05f * evt.Severity);
                    break;
                case SimEventType.FoodShortage:
                    region.Stability = Mathf.Max(0.1f, region.Stability - 0.04f * evt.Severity);
                    break;
                case SimEventType.NaturalDisaster:
                    region.Stability = Mathf.Max(0.1f, region.Stability - 0.1f * evt.Severity);
                    region.Add(ResourceId.Food, -region.Population * 0.08f * evt.Severity);
                    break;
                case SimEventType.LowStability:
                    region.Stability = Mathf.Max(0.1f, region.Stability - 0.02f);
                    break;
            }
        }

        /// <summary>
        /// Forecast major breakpoints inside [fromDay, toDay) without mutating world.
        /// Uses deterministic hash of seed+day+region for reproducibility.
        /// </summary>
        public static List<RegionEvent> ForecastBreakpoints(WorldState world, int fromDay, int toDay, int seed)
        {
            var list = new List<RegionEvent>();
            // Sample each season boundary and mid-season for disaster / disease risk.
            for (int day = fromDay; day < toDay; day += WorldState.DaysPerSeason / 2)
            {
                foreach (var region in world.Regions)
                {
                    float diseaseRisk = region.DiseasePressure;
                    float foodRisk = region.Get(ResourceId.Food) < region.Population * 0.35f ? 0.4f : 0.05f;
                    float roll = Hash01(seed, day, (int)region.Id, 17);
                    if (diseaseRisk >= 0.2f && roll < 0.35f + diseaseRisk)
                    {
                        list.Add(new RegionEvent
                        {
                            EventId = $"fc_disease_{region.Id}_{day}",
                            EventType = SimEventType.DiseaseOutbreak,
                            RegionId = region.Id,
                            StartDay = day,
                            Duration = 30,
                            Severity = Mathf.Clamp01(0.5f + diseaseRisk)
                        });
                    }

                    float roll2 = Hash01(seed, day, (int)region.Id, 91);
                    if (foodRisk > 0.2f && roll2 < foodRisk)
                    {
                        list.Add(new RegionEvent
                        {
                            EventId = $"fc_food_{region.Id}_{day}",
                            EventType = SimEventType.FoodShortage,
                            RegionId = region.Id,
                            StartDay = day,
                            Duration = 20,
                            Severity = 0.7f
                        });
                    }

                    float roll3 = Hash01(seed, day, (int)region.Id, 3);
                    if (roll3 < 0.04f)
                    {
                        list.Add(new RegionEvent
                        {
                            EventId = $"fc_disaster_{region.Id}_{day}",
                            EventType = SimEventType.NaturalDisaster,
                            RegionId = region.Id,
                            StartDay = day,
                            Duration = 15,
                            Severity = 0.8f + roll3
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

            Upsert(region, world, type, duration, severity);
        }

        static void Upsert(RegionState region, WorldState world, SimEventType type, int duration, float severity)
        {
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
                StartDay = world.TotalDays,
                Duration = duration,
                Severity = severity
            });
        }

        static void ExpireFinished(RegionState region, int totalDay)
        {
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
                sb.Append(Label(region.ActiveEvents[i].EventType));
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
                // 2654435761 does not fit in int; keep hash math in uint then cast back.
                uint h = (uint)seed;
                h = h * 73856093u ^ (uint)day * 19349663u;
                h = h * 83492791u ^ (uint)region * 50331653u;
                h = h * 2654435761u ^ (uint)salt;
                h &= 0x7fffffffu;
                return (h % 10000u) / 10000f;
            }
        }
    }
}
