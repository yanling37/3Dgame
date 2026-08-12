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
            SimulationConfig config,
            ObserverInfluence influence,
            int years,
            int seed)
        {
            int targetDay = source.TotalDays + years * WorldState.DaysPerYear;
            return FastForwardToTotalDay(source, races, config, influence, targetDay, seed);
        }

        public static Result FastForwardToTotalDay(
            WorldState source,
            RaceDefinition[] races,
            SimulationConfig config,
            ObserverInfluence influence,
            int targetTotalDay,
            int seed)
        {
            config = config ?? SimulationConfig.CreateDefault();
            var state = source.Clone();
            state.RandomSeed = seed;
            SeasonSystem.SyncFromCalendar(state);
            // Ensure HUD scratch values are applied to region Influence before projection.
            if (influence != null)
            {
                influence.Bind(state);
                influence.PushToFocus();
            }

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
                    ProjectDays(state, races, config, daysToBreak);
                    ApplyBreakpoint(state, breakEvt);
                    breakpoints++;
                    log.AppendLine($"breakpoint day={nextBreak} {breakEvt.EventType} @ {breakEvt.RegionId}");
                }
                else
                {
                    ProjectDays(state, races, config, chunk);
                }
            }

            SeasonSystem.SyncFromCalendar(state);
            return new Result { State = state, BreakpointsApplied = breakpoints, Log = log.ToString() };
        }

        public static void ProjectDays(
            WorldState state,
            RaceDefinition[] races,
            SimulationConfig config,
            int days)
        {
            int left = days;
            while (left > 0)
            {
                SeasonSystem.SyncFromCalendar(state);
                int seasonDaysLeft = WorldState.DaysPerSeason
                    - ((Mathf.Max(0, state.DayOfYear - 1)) % WorldState.DaysPerSeason);
                int step = Mathf.Min(left, Mathf.Max(1, seasonDaysLeft));
                ProjectSeasonChunk(state, races, config, step);
                left -= step;
            }
        }

        static void ProjectSeasonChunk(
            WorldState state,
            RaceDefinition[] races,
            SimulationConfig config,
            int days)
        {
            SeasonSystem.SyncFromCalendar(state);
            var season = state.CurrentSeason;
            float weather = SeasonSystem.WeatherBaseline(season);

            foreach (var region in state.Regions)
            {
                var race = RegionLookup.FindRace(races, region.DominantRace);
                region.WeatherFactor = weather;

                float labor = Mathf.Clamp(region.Population / config.LaborDivisor, config.LaborMin, config.LaborMax);
                float tech = config.TechBase + region.Education * config.TechFromEducation;
                float harvest = region.Influence?.HarvestBlessing ?? 1f;

                ProjectResource(region, ResourceId.Food, race, season, config, labor, tech, weather, harvest, days,
                    config.FoodNeedPerCapita * region.Population,
                    config.FoodBaseSpoilageRate * config.FoodSpoilageModifier(season),
                    float.MaxValue);

                float waterCap = ResourceSystem.GetWaterCapacity(region, season, config);
                region.LastWaterCapacity = waterCap;
                ProjectResource(region, ResourceId.Water, race, season, config, labor, tech, weather, harvest, days,
                    config.WaterNeedPerCapita * region.Population,
                    0f,
                    waterCap);

                ProjectResource(region, ResourceId.Timber, race, season, config, labor, tech, weather, harvest, days, 0f, 0f, float.MaxValue);
                ProjectResource(region, ResourceId.Ore, race, season, config, labor, tech, weather, harvest, days, 0f, 0f, float.MaxValue);
                ProjectResource(region, ResourceId.Faith, race, season, config, labor, tech, weather, harvest, days, 0f, 0f, float.MaxValue);
                ProjectResource(region, ResourceId.Knowledge, race, season, config, labor, tech, weather, harvest, days, 0f, 0f, float.MaxValue);
                ProjectResource(region, ResourceId.Magic, race, season, config, labor, tech, weather, harvest, days, 0f, 0f, float.MaxValue);

                float r = PopulationSystem.EstimateDailyNetGrowthRate(region, race, season, config);
                float K = PopulationSystem.CalculateCarryingCapacity(region, race, season, config);
                region.LastCarryingCapacity = K;
                float p0 = Mathf.Max(config.MinPopulation, region.Population);
                float p1;
                if (Mathf.Abs(r) < 1e-8f)
                {
                    p1 = p0;
                }
                else
                {
                    float growth = r * p0 * (1f - p0 / Mathf.Max(1f, K)) * days;
                    p1 = Mathf.Max(config.MinPopulation, p0 + growth);
                }

                region.PopulationDelta = (p1 - p0) / Mathf.Max(1, days);
                region.Population = p1;

                float diseaseTarget = Mathf.Clamp01(region.DiseasePressure * 0.7f + (1f - SeasonSystem.DiseaseModifier(season) + 1f) * 0.05f);
                if (region.Get(ResourceId.Food) < region.Population * config.FoodShortageRatio)
                {
                    diseaseTarget = Mathf.Max(diseaseTarget, 0.2f);
                }

                region.DiseasePressure = Mathf.Clamp01(Mathf.Lerp(region.DiseasePressure, diseaseTarget, Mathf.Clamp01(days / 90f)));

                float knowledge = region.Get(ResourceId.Knowledge);
                float faith = region.Get(ResourceId.Faith);
                float eduT = Mathf.Clamp01(knowledge / config.KnowledgeEducationDivisor);
                float faithT = Mathf.Clamp01(faith / config.FaithLevelDivisor);
                float t = 1f - Mathf.Pow(1f - config.EducationLerp, days);
                region.Education = Mathf.Clamp01(Mathf.Lerp(region.Education, eduT, t));
                region.FaithLevel = Mathf.Clamp01(Mathf.Lerp(region.FaithLevel, faithT, t));

                if (region.Get(ResourceId.Food) < region.Population * config.FoodShortageRatio)
                {
                    region.Stability = Mathf.Max(0.05f, region.Stability - config.FoodShortageStabilityLoss * days * 0.35f);
                }
                else if (region.Get(ResourceId.Food) > region.Population * config.FoodSurplusRatio)
                {
                    float stabMul = region.Influence?.StabilityBlessing ?? 1f;
                    region.Stability = Mathf.Min(1.5f, region.Stability + config.FoodSurplusStabilityGain * stabMul * days * 0.35f);
                }
            }

            AdvanceCalendar(state, days);
            SeasonSystem.SyncFromCalendar(state);
        }

        static void ProjectResource(
            RegionState region,
            ResourceId id,
            RaceDefinition race,
            SeasonId season,
            SimulationConfig config,
            float labor,
            float tech,
            float env,
            float harvest,
            int days,
            float consumePerDay,
            float spoilRate,
            float capacity)
        {
            float prod = ResourceSystem.EstimateDailyProduction(region, race, id, season, config, labor, tech, env, harvest);
            if (spoilRate > 0f)
            {
                float net = prod - consumePerDay;
                float sInf = net <= 0f ? 0f : net / spoilRate;
                float s0 = region.Get(id);
                float s1 = sInf + (s0 - sInf) * Mathf.Exp(-spoilRate * days);
                region.Set(id, Mathf.Min(capacity, Mathf.Max(0f, s1)));
            }
            else
            {
                float next = region.Get(id) + (prod - consumePerDay) * days;
                region.Set(id, Mathf.Min(capacity, Mathf.Max(0f, next)));
            }
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

        static void ApplyBreakpoint(WorldState state, RegionEvent evt)
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
