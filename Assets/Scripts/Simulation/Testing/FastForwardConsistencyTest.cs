using System.Text;
using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Systems;
using UnityEngine;

namespace DivineWorld.Simulation.Testing
{
    /// <summary>
    /// Daily vs FastForward consistency harness. Reports real errors; does not hide SOFT PASS issues.
    /// </summary>
    public static class FastForwardConsistencyTest
    {
        public const float HardErrorTarget = 0.05f;

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
            public bool WithinHardTarget;
            public bool Finite;
            public string Text;
        }

        public static Report Run(
            WorldState initial,
            RaceDefinition[] races,
            SimulationConfig config,
            int days)
        {
            var daily = initial.Clone();
            var rng = new System.Random(daily.RandomSeed);
            for (int d = 0; d < days; d++)
            {
                DailySimulation.SimulateDay(daily, races, config, rng);
                if (daily.HaltedOnNumericError)
                {
                    break;
                }
            }

            var fast = FastForwardSystem.FastForwardToTotalDay(initial, races, config, initial.TotalDays + days);
            var metrics = BuildMetrics(daily, fast.State);
            bool ok = true;
            bool finite = IsFiniteWorld(daily) && IsFiniteWorld(fast.State);
            var sb = new StringBuilder();
            sb.AppendLine($"=== Daily {days}d VS FastForward {days}d ===");
            sb.AppendLine(fast.Log);
            if (daily.HaltedOnNumericError)
            {
                sb.AppendLine("DAILY HALTED ON NUMERIC ERROR");
                sb.AppendLine(daily.LastNumericError);
                finite = false;
            }

            if (fast.State.HaltedOnNumericError)
            {
                sb.AppendLine("FAST HALTED ON NUMERIC ERROR");
                sb.AppendLine(fast.State.LastNumericError);
                finite = false;
            }

            foreach (var m in metrics)
            {
                sb.AppendLine($"{m.Name}: daily={m.Daily:0.##} fast={m.Fast:0.##} diff={m.AbsDiff:0.##} error={m.ErrorPct * 100f:0.0}%");
                if (m.ErrorPct > HardErrorTarget)
                {
                    ok = false;
                }
            }

            sb.AppendLine(finite
                ? (ok ? "HARD PASS (all errors < 5%)" : "SOFT PASS / FAIL vs 5% target — real errors shown above")
                : "NUMERIC FAIL (NaN/Infinity detected)");
            Debug.Log(sb.ToString());
            return new Report { Metrics = metrics, WithinHardTarget = ok && finite, Finite = finite, Text = sb.ToString() };
        }

        static bool IsFiniteWorld(WorldState world)
        {
            if (world?.Regions == null)
            {
                return false;
            }

            foreach (var r in world.Regions)
            {
                if (!NumericGuard.IsFinite(r.Population) || r.Population < 0f) return false;
                if (!NumericGuard.IsFinite(r.Stability)) return false;
                if (!NumericGuard.IsFinite(r.DiseasePressure)) return false;
                foreach (ResourceId id in System.Enum.GetValues(typeof(ResourceId)))
                {
                    float v = r.Get(id);
                    if (!NumericGuard.IsFinite(v) || v < 0f) return false;
                }
            }

            return true;
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
            // Both near-zero ⇒ 0% error (e.g. Water depleted).
            if (Mathf.Abs(daily) < 1e-3f && Mathf.Abs(fast) < 1e-3f)
            {
                return new Metric { Name = name, Daily = daily, Fast = fast, AbsDiff = 0f, ErrorPct = 0f };
            }

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
