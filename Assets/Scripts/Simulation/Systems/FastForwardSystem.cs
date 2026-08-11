using System.Collections.Generic;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Mathematical / seasonal fast-forward. Does NOT call TickDay in a loop.
    /// </summary>
    public static class FastForwardSystem
    {
        public struct Result
        {
            public WorldState State;
            public int BreakpointsApplied;
            public string Log;
        }

        public static Result FastForwardYears(
            WorldState source,
            RaceDefinition[] races,
            ObserverInfluence influence,
            int years,
            int seed)
        {
            int targetDay = source.TotalDays + years * WorldState.DaysPerYear;
            return FastForwardToTotalDay(source, races, influence, targetDay, seed);
        }

        public static Result FastForwardToTotalDay(
            WorldState source,
            RaceDefinition[] races,
            ObserverInfluence influence,
            int targetTotalDay,
            int seed)
        {
            var state = source.Clone();
            state.RandomSeed = seed;
            SeasonSystem.SyncFromCalendar(state);

            int breakpoints = 0;
            var log = new System.Text.StringBuilder();
            log.AppendLine($"FastForward {state.TotalDays} → {targetTotalDay}");

            while (state.TotalDays < targetTotalDay)
            {
                int remaining = targetTotalDay - state.TotalDays;
                int seasonDaysLeft = WorldState.DaysPerSeason
                    - ((Mathf.Max(0, state.DayOfYear - 1)) % WorldState.DaysPerSeason);
                int chunk = Mathf.Min(remaining, seasonDaysLeft);
                if (chunk <= 0) chunk = Mathf.Min(remaining, WorldState.DaysPerSeason);

                int windowEnd = state.TotalDays + chunk;
                var forecast = EventSystem.ForecastBreakpoints(state, state.TotalDays + 1, windowEnd, seed);
                int nextBreak = -1;
                RegionEvent breakEvt = null;
                for (int i = 0; i < forecast.Count; i++)
                {
                    if (forecast[i].StartDay > state.TotalDays && forecast[i].StartDay < windowEnd)
                    {
                        nextBreak = forecast[i].StartDay;
                        breakEvt = forecast[i];
                        break;
                    }
                }

                if (nextBreak > 0)
                {
                    int daysToBreak = nextBreak - state.TotalDays;
                    ProjectDays(state, races, influence, daysToBreak);
                    ApplyBreakpoint(state, races, breakEvt);
                    breakpoints++;
                    log.AppendLine($"breakpoint day={nextBreak} {breakEvt.EventType} @ {breakEvt.RegionId}");
                }
                else
                {
                    ProjectDays(state, races, influence, chunk);
                }
            }

            SeasonSystem.SyncFromCalendar(state);
            return new Result { State = state, BreakpointsApplied = breakpoints, Log = log.ToString() };
        }

        /// <summary>
        /// Macro-project an arbitrary day count by splitting into season-aligned pieces.
        /// </summary>
        public static void ProjectDays(
            WorldState state,
            RaceDefinition[] races,
            ObserverInfluence influence,
            int days)
        {
            int left = days;
            while (left > 0)
            {
                SeasonSystem.SyncFromCalendar(state);
                int seasonDaysLeft = WorldState.DaysPerSeason
                    - ((Mathf.Max(0, state.DayOfYear - 1)) % WorldState.DaysPerSeason);
                int step = Mathf.Min(left, Mathf.Max(1, seasonDaysLeft));
                ProjectSeasonChunk(state, races, influence, step);
                left -= step;
            }
        }

        static void ProjectSeasonChunk(
            WorldState state,
            RaceDefinition[] races,
            ObserverInfluence influence,
            int days)
        {
            SeasonSystem.SyncFromCalendar(state);
            var season = state.CurrentSeason;
            float weather = SeasonSystem.WeatherBaseline(season);

            foreach (var region in state.Regions)
            {
                var race = RegionLookup.FindRace(races, region.DominantRace);
                region.WeatherFactor = weather;

                float labor = Mathf.Clamp(region.Population / 10000f, 0.2f, 8f);
                float tech = 0.6f + region.Education * 0.8f;
                float harvest = influence.RegionMultiplier(region.Id, influence.HarvestBlessing);

                // Resources: integrate daily rates over `days` (stable-state clamp for perishables).
                foreach (var def in ResourceCatalog.All)
                {
                    float prod = ResourceSystem.EstimateDailyProduction(
                        region, race, def, season, labor, tech, weather, harvest);
                    float consume = def.BaseConsumePerCapita * region.Population;
                    float spoilRate = def.CanSpoil ? def.BaseSpoilRate : 0f;

                    if (def.CanSpoil && spoilRate > 0f)
                    {
                        // Continuous approx: dS/dt = P - C - s*S → S_inf = (P-C)/s
                        float net = prod - consume;
                        float sInf = net <= 0f ? 0f : net / spoilRate;
                        float s0 = region.Get(def.Id);
                        float s1 = sInf + (s0 - sInf) * Mathf.Exp(-spoilRate * days);
                        region.Set(def.Id, Mathf.Max(0f, s1));
                    }
                    else
                    {
                        region.Add(def.Id, (prod - consume) * days);
                    }
                }

                // Population logistic with carrying capacity.
                float r = PopulationSystem.EstimateDailyNetGrowthRate(region, race, influence, season);
                float K = ResourceSystem.EstimateCarryingCapacity(region, race, season);
                float p0 = Mathf.Max(100f, region.Population);
                float p1;
                if (Mathf.Abs(r) < 1e-8f)
                {
                    p1 = p0;
                }
                else
                {
                    // Discrete logistic: P' = P + r*P*(1-P/K)*days  with soft clamp
                    float growth = r * p0 * (1f - p0 / Mathf.Max(1f, K)) * days;
                    p1 = Mathf.Max(100f, p0 + growth);
                }

                region.PopulationDelta = (p1 - p0) / Mathf.Max(1, days);
                region.Population = p1;

                // Soft disease drift toward seasonal pressure.
                float diseaseTarget = Mathf.Clamp01(region.DiseasePressure * 0.7f + (1f - SeasonSystem.DiseaseModifier(season) + 1f) * 0.05f);
                if (region.Get(ResourceId.Food) < region.Population * 0.25f)
                {
                    diseaseTarget = Mathf.Max(diseaseTarget, 0.2f);
                }

                region.DiseasePressure = Mathf.Clamp01(Mathf.Lerp(region.DiseasePressure, diseaseTarget, Mathf.Clamp01(days / 90f)));

                float knowledge = region.Get(ResourceId.Knowledge);
                float faith = region.Get(ResourceId.Faith);
                float eduT = Mathf.Clamp01(knowledge / 20000f);
                float faithT = Mathf.Clamp01(faith / 25000f);
                float t = 1f - Mathf.Pow(1f - 0.002f, days);
                region.Education = Mathf.Clamp01(Mathf.Lerp(region.Education, eduT, t));
                region.FaithLevel = Mathf.Clamp01(Mathf.Lerp(region.FaithLevel, faithT, t));

                if (region.Get(ResourceId.Food) < region.Population * 0.2f)
                {
                    region.Stability = Mathf.Max(0.05f, region.Stability - 0.004f * days * 0.35f);
                }
                else if (region.Get(ResourceId.Food) > region.Population * 0.8f)
                {
                    region.Stability = Mathf.Min(1.5f, region.Stability + 0.0015f * days * 0.35f);
                }
            }

            AdvanceCalendar(state, days);
            SeasonSystem.SyncFromCalendar(state);
        }

        static void AdvanceCalendar(WorldState state, int days)
        {
            state.TotalDays += days;
            state.DayOfYear += days;
            while (state.DayOfYear > WorldState.DaysPerYear)
            {
                state.DayOfYear -= WorldState.DaysPerYear;
                state.Year++;
            }
        }

        static void ApplyBreakpoint(WorldState state, RaceDefinition[] races, RegionEvent evt)
        {
            for (int i = 0; i < state.Regions.Length; i++)
            {
                if (state.Regions[i].Id != evt.RegionId)
                {
                    continue;
                }

                var region = state.Regions[i];
                region.ActiveEvents.RemoveAll(e => e.EventType == evt.EventType);
                region.ActiveEvents.Add(evt);
                EventSystem.ApplyEventImpact(region, evt);
                region.LastEvent = EventSystem.Label(evt.EventType);
                return;
            }
        }
    }
}
