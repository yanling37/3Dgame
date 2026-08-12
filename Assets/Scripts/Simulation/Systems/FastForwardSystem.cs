using System.Collections.Generic;
using System.Text;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Event-breakpoint + interval projection FastForward.
    /// Does not loop full DailySimulation; uses shared rate estimators and closed-form resource updates.
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
            int years)
        {
            int target = source.TotalDays + years * SimulationConfig.DaysPerYear;
            return FastForwardToTotalDay(source, races, config, target);
        }

        public static Result FastForwardToTotalDay(
            WorldState source,
            RaceDefinition[] races,
            SimulationConfig config,
            int targetTotalDay)
        {
            var state = source.Clone();
            state.SyncSeasonFromDay();
            int breakpoints = 0;
            var log = new StringBuilder();
            log.AppendLine($"FastForward {state.TotalDays} → {targetTotalDay}");

            while (state.TotalDays < targetTotalDay && !state.HaltedOnNumericError)
            {
                int remaining = targetTotalDay - state.TotalDays;
                int seasonDaysLeft = SimulationConfig.DaysPerSeason - ((Mathf.Max(0, state.DayOfYear - 1)) % SimulationConfig.DaysPerSeason);
                int seasonChunk = Mathf.Min(remaining, Mathf.Max(1, seasonDaysLeft));
                int windowEnd = state.TotalDays + seasonChunk;

                var forecast = EventSystem.ForecastBreakpoints(state, state.TotalDays, windowEnd, config);
                int nextBreak = -1;
                RegionEvent breakEvt = null;
                for (int i = 0; i < forecast.Count; i++)
                {
                    if (forecast[i].StartDay > state.TotalDays && forecast[i].StartDay <= windowEnd)
                    {
                        nextBreak = forecast[i].StartDay;
                        breakEvt = forecast[i];
                        break;
                    }
                }

                // Also break early on predicted food/water threshold crossings inside the window.
                int thresholdBreak = FindThresholdBreakpoint(state, races, config, seasonChunk);
                if (thresholdBreak > 0 && (nextBreak < 0 || thresholdBreak < nextBreak))
                {
                    nextBreak = thresholdBreak;
                    breakEvt = null;
                }

                if (nextBreak > state.TotalDays && nextBreak <= windowEnd)
                {
                    int daysToBreak = nextBreak - state.TotalDays;
                    if (daysToBreak > 0)
                    {
                        ProjectDays(state, races, config, daysToBreak);
                    }

                    if (breakEvt != null)
                    {
                        ApplyBreakpoint(state, breakEvt, config);
                        breakpoints++;
                        log.AppendLine($"breakpoint day={nextBreak} {breakEvt.EventType} @ {breakEvt.RegionId}");
                    }
                    else
                    {
                        breakpoints++;
                        log.AppendLine($"breakpoint day={nextBreak} Threshold");
                    }
                }
                else
                {
                    ProjectDays(state, races, config, seasonChunk);
                }
            }

            state.SyncSeasonFromDay();
            return new Result { State = state, BreakpointsApplied = breakpoints, Log = log.ToString() };
        }

        public static void ProjectDays(
            WorldState state,
            RaceDefinition[] races,
            SimulationConfig config,
            int days)
        {
            int left = days;
            int sub = Mathf.Max(1, config.FastForwardSubchunkDays);
            while (left > 0 && !state.HaltedOnNumericError)
            {
                state.SyncSeasonFromDay();
                int seasonDaysLeft = SimulationConfig.DaysPerSeason
                    - ((Mathf.Max(0, state.DayOfYear - 1)) % SimulationConfig.DaysPerSeason);
                int step = Mathf.Min(left, Mathf.Min(sub, Mathf.Max(1, seasonDaysLeft)));
                ProjectChunk(state, races, config, step);
                left -= step;
            }
        }

        static void ProjectChunk(
            WorldState state,
            RaceDefinition[] races,
            SimulationConfig config,
            int days)
        {
            state.SyncSeasonFromDay();
            var season = state.CurrentSeason;

            // FastForward uses seasonal weather baseline (expected value of daily pull+noise).
            config.GetWeatherRange(season, out float baseline, out float min, out float max);
            float weather = Mathf.Clamp(baseline, min, max);

            foreach (var region in state.Regions)
            {
                var race = RegionLookup.FindRace(races, region.DominantRace);
                if (race == null)
                {
                    continue;
                }

                region.WeatherFactor = weather;
                ExpireEvents(region, state.TotalDays);

                float labor = ResourceSystem.ComputeLabor(region, config);
                float tech = config.TechBase + region.Education * config.TechFromEducation;
                float harvest = Sanitize(region.Influence?.HarvestBlessing ?? 1f, 1f);
                float eventMul = EventSystem.GetFoodProductionEventModifier(region, state.TotalDays, config);

                float unconstrained = ResourceSystem.CalculateUnconstrainedFoodProduction(
                    region, race, season, config, labor, tech, weather, harvest) * eventMul;
                ResourceSystem.ResolveWaterAllocation(
                    region, unconstrained, config,
                    out float waterFactor, out float livingUsed, out float agriUsed, out float waterCons);
                float foodProd = unconstrained * waterFactor;
                float foodCons = ResourceSystem.DailyFoodConsumption(region, config);
                float spoilRate = config.FoodBaseSpoilageRate * config.FoodSpoilageModifier(season);

                // Closed-form perishable: S_{n+1} = a*(S_n + P - C), a=1-s
                ProjectPerishable(region, ResourceId.Food, foodProd, foodCons, spoilRate, days);
                region.LastFoodProduction = foodProd;
                region.LastWaterFactor = waterFactor;
                region.LastFoodReserveDays = ResourceSystem.GetFoodReserveDays(region, config);

                float waterProd = Mathf.Max(0f, region.GetProductionCapacity(ResourceId.Water) * config.WaterProductionRate * weather
                    * (season == SeasonId.Winter ? 0.7f : season == SeasonId.Summer ? 0.85f : 1f));
                float waterCap = ResourceSystem.GetWaterCapacity(region, season, config);
                region.LastWaterCapacity = waterCap;
                ProjectCapacityLimited(region, ResourceId.Water, waterProd, waterCons, waterCap, days);

                ProjectPersistent(region, ResourceId.Timber,
                    race.PrefersSea ? config.SeaTimberFlatYield
                        : region.GetProductionCapacity(ResourceId.Timber) * config.TimberLaborScale * labor * tech,
                    0f, days);
                ProjectPersistent(region, ResourceId.Ore,
                    race.PrefersSea ? config.SeaOreFlatYield
                        : region.GetProductionCapacity(ResourceId.Ore) * config.OreLaborScale * labor * tech,
                    0f, days);

                // Soft-saturated persistent yields integrated approximately with mid-point stock.
                ProjectSoftPersistent(region, ResourceId.Faith,
                    region.Population * config.FaithYieldPerCapita * region.FaithLevel * race.FaithTendency,
                    config.FaithStockSoftCap, days);
                ProjectSoftPersistent(region, ResourceId.Knowledge,
                    region.Population * config.KnowledgeYieldPerCapita * region.Education * race.KnowledgeTendency,
                    config.KnowledgeStockSoftCap, days);
                ProjectSoftPersistent(region, ResourceId.Magic,
                    region.Population * config.MagicYieldPerCapita * race.MagicAffinity
                    * (race.PrefersSea ? config.SeaMagicAffinityBonus : config.LandMagicAffinityBonus),
                    config.MagicStockSoftCap, days);

                // Population: discrete logistic recurrence for `days` with frozen K/rates, then re-eval via subchunks.
                float k = PopulationSystem.CalculateCarryingCapacity(region, race, season, config);
                region.LastCarryingCapacity = k;
                float p0 = Mathf.Max(0f, region.Population);
                float p = p0;
                float foodRatio = p <= 0f ? 0f : Mathf.Clamp01(region.Get(ResourceId.Food) / Mathf.Max(1f, p * config.FoodRatioSoftCap));
                float fertility = config.BaseFertility * race.FertilityFactor * Sanitize(region.Influence?.FertilityBlessing ?? 1f, 1f);
                float deathBase = (config.BaseNaturalDeath / Mathf.Max(0.05f, race.LifespanFactor)) * config.DeathModifier(season)
                                  + region.DiseasePressure * config.DiseaseDeathRate * config.DiseaseModifier(season)
                                    * Sanitize(region.Influence?.DiseasePressure ?? 1f, 1f);

                for (int i = 0; i < days; i++)
                {
                    if (p <= 0f)
                    {
                        p = 0f;
                        break;
                    }

                    float logistic = Mathf.Max(0f, 1f - p / Mathf.Max(1f, k));
                    float birth = p * fertility * (0.5f + foodRatio) * logistic;
                    float over = p > k ? p * config.OverpopulationDeathRate * (p / k - 1f) : 0f;
                    float next = p + birth - p * deathBase - over;
                    if (!NumericGuard.AcceptOrHalt(state, region, "Population", p, next, $"fastforward chunk day={i}, K={k}"))
                    {
                        return;
                    }

                    p = Mathf.Max(0f, next);
                }

                region.PopulationDelta = days > 0 ? (p - p0) / days : 0f;
                region.Population = p;

                // Disease drift — mirror daily: decay + summer gain + food-shortage pressure.
                float disease = region.DiseasePressure;
                float summerGain = season == SeasonId.Summer
                    ? 0.003f * config.DiseaseModifier(season) * Sanitize(region.Influence?.DiseasePressure ?? 1f, 1f)
                    : 0f;
                float foodShortageGain = 0f;
                float reserve = ResourceSystem.GetFoodReserveDays(region, config);
                if (region.Get(ResourceId.Food) < region.Population * config.FoodShortageRatio
                    || reserve < config.FoodShortageReserveDays)
                {
                    foodShortageGain = config.FoodShortageDiseaseGain
                        * Sanitize(region.Influence?.DiseasePressure ?? 1f, 1f);
                    region.Stability = Mathf.Max(0.05f, region.Stability - config.FoodShortageStabilityLoss * days);
                }
                else if (region.Get(ResourceId.Food) > region.Population * config.FoodSurplusRatio)
                {
                    region.Stability = Mathf.Min(3f, region.Stability + config.FoodSurplusStabilityGain * days
                        * Sanitize(region.Influence?.StabilityBlessing ?? 1f, 1f));
                }

                for (int i = 0; i < days; i++)
                {
                    disease = Mathf.Clamp01(disease * config.DiseaseDecay + summerGain + foodShortageGain);
                }

                region.DiseasePressure = disease;

                // Education / faith level drift (deterministic, no unrest RNG).
                float knowledge = region.Get(ResourceId.Knowledge);
                float faithStock = region.Get(ResourceId.Faith);
                float eduT = Mathf.Clamp01(knowledge / config.KnowledgeEducationDivisor);
                float faithT = Mathf.Clamp01(faithStock / config.FaithLevelDivisor);
                float t = 1f - Mathf.Pow(1f - config.EducationLerp, days);
                region.Education = Mathf.Clamp01(Mathf.Lerp(region.Education, eduT, t));
                region.FaithLevel = Mathf.Clamp01(Mathf.Lerp(region.FaithLevel, faithT, t));

                region.LastLivingWaterUsed = livingUsed;
                region.LastAgriculturalWaterUsed = agriUsed;
            }

            AdvanceCalendar(state, days);
            // Expire events after advancing so duration matches daily TotalDays semantics.
            foreach (var region in state.Regions)
            {
                ExpireEvents(region, state.TotalDays);
                region.LastEvent = SummarizeEvents(region);
            }
        }

        static int FindThresholdBreakpoint(
            WorldState state,
            RaceDefinition[] races,
            SimulationConfig config,
            int maxDays)
        {
            // Peek 1-day projection rates to estimate when food reserve / water hit critical levels.
            foreach (var region in state.Regions)
            {
                var race = RegionLookup.FindRace(races, region.DominantRace);
                if (race == null || region.Population <= 0f)
                {
                    continue;
                }

                float foodCons = ResourceSystem.DailyFoodConsumption(region, config);
                float reserve = ResourceSystem.GetFoodReserveDays(region, config);
                if (reserve < config.FastForwardFoodReserveBreakpointDays)
                {
                    // Re-linearize soon, but not every single day (avoid FF→daily clone spam).
                    int step = Mathf.Max(1, config.FastForwardSubchunkDays);
                    return state.TotalDays + step;
                }

                // If net food strongly negative, estimate days until reserve threshold.
                float labor = ResourceSystem.ComputeLabor(region, config);
                float tech = config.TechBase + region.Education * config.TechFromEducation;
                state.SyncSeasonFromDay();
                float unconstrained = ResourceSystem.CalculateUnconstrainedFoodProduction(
                    region, race, state.CurrentSeason, config, labor, tech, region.WeatherFactor,
                    Sanitize(region.Influence?.HarvestBlessing ?? 1f, 1f));
                ResourceSystem.ResolveWaterAllocation(region, unconstrained, config, out float wf, out _, out _, out _);
                float net = unconstrained * wf - foodCons;
                float spoil = config.FoodBaseSpoilageRate * config.FoodSpoilageModifier(state.CurrentSeason);
                // Rough days to hit threshold reserve days worth of stock.
                float targetStock = foodCons * config.FastForwardFoodReserveBreakpointDays;
                float stock = region.Get(ResourceId.Food);
                if (net < -1f && stock > targetStock)
                {
                    float daysTo = (stock - targetStock) / (-net + spoil * stock + 1e-3f);
                    if (daysTo > 0f && daysTo < maxDays)
                    {
                        return state.TotalDays + Mathf.Max(1, Mathf.CeilToInt(daysTo));
                    }
                }

                if (region.Get(ResourceId.Water) <= 1f && region.GetProductionCapacity(ResourceId.Water) > 0f)
                {
                    return state.TotalDays + Mathf.Max(1, config.FastForwardSubchunkDays);
                }
            }

            return -1;
        }

        static void ApplyBreakpoint(WorldState state, RegionEvent evt, SimulationConfig config)
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
                EventSystem.ApplyEventImpact(region, evt, config, initialImpact: true);
                region.LastEvent = EventSystem.Label(evt.EventType);
                return;
            }
        }

        static void ProjectPerishable(RegionState region, ResourceId id, float prod, float cons, float spoilRate, int days)
        {
            float a = 1f - Mathf.Clamp(spoilRate, 0f, 0.999f);
            float b = a * (prod - cons);
            float s = region.Get(id);
            if (Mathf.Abs(1f - a) < 1e-8f)
            {
                s = s + (prod - cons) * days;
            }
            else
            {
                // S_n = a^n * S_0 + b * (1 - a^n) / (1 - a)
                float an = Mathf.Pow(a, days);
                s = an * s + b * (1f - an) / (1f - a);
            }

            region.Set(id, Mathf.Max(0f, s));
        }

        static void ProjectCapacityLimited(RegionState region, ResourceId id, float prod, float cons, float capacity, int days)
        {
            float net = prod - cons;
            float s = region.Get(id) + net * days;
            region.Set(id, Mathf.Clamp(s, 0f, Mathf.Max(0f, capacity)));
        }

        static void ProjectPersistent(RegionState region, ResourceId id, float prod, float cons, int days)
        {
            region.Set(id, Mathf.Max(0f, region.Get(id) + (prod - cons) * days));
        }

        static void ProjectSoftPersistent(RegionState region, ResourceId id, float rawProd, float softCap, int days)
        {
            float s = region.Get(id);
            // Integrate ds/dt = raw / (1 + s/cap) ⇒ (s + s^2/(2cap))' = raw
            // Closed form via quadratic: s + s^2/(2c) = s0 + s0^2/(2c) + raw*days
            float c = Mathf.Max(1f, softCap);
            float u0 = s + (s * s) / (2f * c);
            float u1 = u0 + Mathf.Max(0f, rawProd) * days;
            // Solve s^2/(2c) + s - u1 = 0 → s = -c + sqrt(c^2 + 2c*u1)
            float disc = c * c + 2f * c * u1;
            float s1 = disc > 0f ? -c + Mathf.Sqrt(disc) : 0f;
            region.Set(id, Mathf.Max(0f, s1));
        }

        static void AdvanceCalendar(WorldState state, int days)
        {
            for (int i = 0; i < days; i++)
            {
                state.DayOfYear++;
                state.TotalDays++;
                if (state.DayOfYear > SimulationConfig.DaysPerYear)
                {
                    state.DayOfYear = 1;
                    state.Year++;
                    EventSystem.ApplyYearTurn(state);
                }
            }

            state.SyncSeasonFromDay();
        }

        static void ExpireEvents(RegionState region, int totalDay)
        {
            if (region.ActiveEvents == null)
            {
                return;
            }

            region.ActiveEvents.RemoveAll(e => !e.IsActiveOn(totalDay));
        }

        static string SummarizeEvents(RegionState region)
        {
            if (region.ActiveEvents == null || region.ActiveEvents.Count == 0)
            {
                return region.LastEvent;
            }

            var sb = new StringBuilder();
            for (int i = 0; i < region.ActiveEvents.Count; i++)
            {
                if (i > 0) sb.Append(" · ");
                sb.Append(EventSystem.Label(region.ActiveEvents[i].EventType));
            }

            return sb.ToString();
        }

        static float Sanitize(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return fallback;
            }

            return value;
        }
    }
}
