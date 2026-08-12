using System.Text;
using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using DivineWorld.Simulation.Systems;
using UnityEngine;

namespace DivineWorld.Simulation.Testing
{
    /// <summary>
    /// Compare Daily 1y vs FastForward 1y for the same seed/state.
    /// </summary>
    public static class FastForwardConsistencyTest
    {
        public const float PopErrorSoft = 0.15f;
        public const float ResourceErrorSoft = 0.25f;

        public struct Metric
        {
            public string Name;
            public float Daily;
            public float Fast;
            public float AbsDiff;
            public float ErrorPct;
        }

        public struct Report
        {
            public Metric[] Metrics;
            public bool WithinSoftThresholds;
            public string Text;
        }

        public static Report RunOneYear(
            WorldState initial,
            RaceDefinition[] races,
            SimulationConfig config,
            ObserverInfluence influence,
            int seed)
        {
            config = config ?? SimulationConfig.CreateDefault();
            var dailyWorld = initial.Clone();
            dailyWorld.RandomSeed = seed;
            SeasonSystem.SyncFromCalendar(dailyWorld);
            var rng = new System.Random(seed);

            if (influence != null)
            {
                influence.Bind(dailyWorld);
                influence.PushToFocus();
            }

            for (int d = 0; d < WorldState.DaysPerYear; d++)
            {
                TickOneDay(dailyWorld, races, config, rng);
            }

            var fast = FastForwardSystem.FastForwardYears(initial, races, config, influence, 1, seed);

            var metrics = BuildMetrics(dailyWorld, fast.State);
            bool ok = true;
            var sb = new StringBuilder();
            sb.AppendLine("=== Daily 1y VS FastForward 1y ===");
            sb.AppendLine(fast.Log);
            foreach (var m in metrics)
            {
                sb.AppendLine($"{m.Name}: daily={m.Daily:0.##} fast={m.Fast:0.##} diff={m.AbsDiff:0.##} err={m.ErrorPct * 100f:0.0}%");
                if (m.Name.StartsWith("Pop") && m.ErrorPct > PopErrorSoft) ok = false;
                if ((m.Name.Contains("Food") || m.Name.Contains("Mana") || m.Name.Contains("Water")) && m.ErrorPct > ResourceErrorSoft)
                {
                    ok = false;
                }
            }

            sb.AppendLine(ok ? "SOFT PASS (within thresholds)" : "SOFT WARN (outside thresholds — approximate model)");
            Debug.Log(sb.ToString());
            return new Report { Metrics = metrics, WithinSoftThresholds = ok, Text = sb.ToString() };
        }

        public static void TickOneDay(WorldState state, RaceDefinition[] races, SimulationConfig config, System.Random rng)
        {
            DailySimulation.SimulateDay(state, races, config, rng);
            bool yearTurned = SeasonSystem.AdvanceCalendar(state);
            if (yearTurned)
            {
                EventSystem.ApplyYearTurn(state);
            }

            EventSystem.EvaluateAndApply(state, rng);
            SeasonSystem.SyncFromCalendar(state);
        }

        static Metric[] BuildMetrics(WorldState daily, WorldState fast)
        {
            float dPop = 0, fPop = 0, dFood = 0, fFood = 0, dMana = 0, fMana = 0, dWater = 0, fWater = 0;
            float dDis = 0, fDis = 0, dStab = 0, fStab = 0, dEdu = 0, fEdu = 0, dFaith = 0, fFaith = 0;
            int n = daily.Regions.Length;
            for (int i = 0; i < n; i++)
            {
                dPop += daily.Regions[i].Population;
                fPop += fast.Regions[i].Population;
                dFood += daily.Regions[i].Get(ResourceId.Food);
                fFood += fast.Regions[i].Get(ResourceId.Food);
                dMana += daily.Regions[i].Get(ResourceId.Magic);
                fMana += fast.Regions[i].Get(ResourceId.Magic);
                dWater += daily.Regions[i].Get(ResourceId.Water);
                fWater += fast.Regions[i].Get(ResourceId.Water);
                dDis += daily.Regions[i].DiseasePressure;
                fDis += fast.Regions[i].DiseasePressure;
                dStab += daily.Regions[i].Stability;
                fStab += fast.Regions[i].Stability;
                dEdu += daily.Regions[i].Education;
                fEdu += fast.Regions[i].Education;
                dFaith += daily.Regions[i].FaithLevel;
                fFaith += fast.Regions[i].FaithLevel;
            }

            return new[]
            {
                MetricOf("PopTotal", dPop, fPop),
                MetricOf("FoodTotal", dFood, fFood),
                MetricOf("ManaTotal", dMana, fMana),
                MetricOf("WaterTotal", dWater, fWater),
                MetricOf("DiseaseAvg", dDis / n, fDis / n),
                MetricOf("StabilityAvg", dStab / n, fStab / n),
                MetricOf("EducationAvg", dEdu / n, fEdu / n),
                MetricOf("FaithAvg", dFaith / n, fFaith / n)
            };
        }

        static Metric MetricOf(string name, float daily, float fast)
        {
            float abs = Mathf.Abs(daily - fast);
            float denom = Mathf.Max(1f, Mathf.Abs(daily));
            return new Metric
            {
                Name = name,
                Daily = daily,
                Fast = fast,
                AbsDiff = abs,
                ErrorPct = abs / denom
            };
        }
    }
}
