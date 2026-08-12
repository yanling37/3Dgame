using System;
using System.Text;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using DivineWorld.Simulation.Systems;
using UnityEngine;

namespace DivineWorld.Simulation.Core
{
    /// <summary>
    /// Owns world state and advances simulation ticks (P2-A calendar / season / resources / population / events).
    /// </summary>
    public class SimulationWorld : MonoBehaviour
    {
        [SerializeField] int seed = 20260810;
        [SerializeField] float secondsPerDay = 0.35f;
        [SerializeField] bool autoRun = true;
        [SerializeField, Range(1, 30)] int daysPerFrameWhenFast = 1;

        public WorldState State { get; private set; }
        public RaceDefinition[] Races { get; private set; }
        public SimulationConfig Config { get; private set; }
        public ObserverInfluence Influence { get; private set; } = new ObserverInfluence();

        public int CurrentYear => State?.Year ?? 1;
        public int DayOfYear => State?.DayOfYear ?? 1;
        public SeasonId CurrentSeason => State?.CurrentSeason ?? SeasonId.Spring;
        public int DayInSeason => State?.DayInSeason ?? 1;
        public float SeasonProgress => State?.SeasonProgress ?? 0f;

        public bool AutoRun
        {
            get => autoRun;
            set => autoRun = value;
        }

        public float SecondsPerDay
        {
            get => secondsPerDay;
            set => secondsPerDay = Mathf.Max(0.05f, value);
        }

        public event Action<WorldState> OnDayAdvanced;

        System.Random _rng;
        float _dayTimer;

        void Awake()
        {
            ResetWorld();
        }

        void Update()
        {
            if (!autoRun || State == null || State.HaltedOnNumericError)
            {
                return;
            }

            _dayTimer += Time.deltaTime;
            while (_dayTimer >= secondsPerDay)
            {
                _dayTimer -= secondsPerDay;
                for (int i = 0; i < daysPerFrameWhenFast; i++)
                {
                    AdvanceDay();
                }
            }
        }

        public void ResetWorld()
        {
            _rng = new System.Random(seed);
            Config = SimulationConfig.CreateDefault();
            Races = DefaultWorldFactory.CreateRaces();
            State = DefaultWorldFactory.CreateWorld();
            State.RandomSeed = seed;
            Influence = new ObserverInfluence();
            Influence.Bind(State);
            _dayTimer = 0f;
            OnDayAdvanced?.Invoke(State);
        }

        public void AdvanceDay()
        {
            if (State == null || State.HaltedOnNumericError)
            {
                return;
            }

            DailySimulation.SimulateDay(State, Races, Config, _rng);
            OnDayAdvanced?.Invoke(State);
        }

        public void AdvanceDays(int days)
        {
            for (int i = 0; i < days; i++)
            {
                AdvanceDay();
                if (State != null && State.HaltedOnNumericError)
                {
                    break;
                }
            }
        }

        public FastForwardSystem.Result FastForwardYears(int years)
        {
            if (State == null || Config == null)
            {
                return default;
            }

            var result = FastForwardSystem.FastForwardYears(State, Races, Config, years);
            State = result.State;
            Influence.Bind(State);
            OnDayAdvanced?.Invoke(State);
            return result;
        }

        public string BuildStatusReport()
        {
            if (State == null)
            {
                return "世界未初始化";
            }

            Influence?.PullFromFocus();

            var sb = new StringBuilder(1400);
            sb.AppendLine($"【{State.WorldName}】 年份 {State.Year}  第 {State.DayOfYear} 日  (累计 {State.TotalDays} 日)");
            sb.AppendLine($"季节 {State.CurrentSeason}  季内第 {State.DayInSeason} 日  进度 {State.SeasonProgress:0%}");
            if (State.HaltedOnNumericError)
            {
                sb.AppendLine("!! NUMERIC HALT !!");
                sb.AppendLine(State.LastNumericError);
            }

            sb.AppendLine($"注视焦点: {(Influence.FocusRegion.HasValue ? Influence.FocusRegion.Value.ToString() : "全域")}");
            sb.AppendLine($"微调(焦点): 生育×{Influence.FertilityBlessing:0.00} 收成×{Influence.HarvestBlessing:0.00} 疫病×{Influence.DiseaseCurse:0.00} 稳定×{Influence.StabilityBlessing:0.00}");
            sb.AppendLine();

            foreach (var r in State.Regions)
            {
                var race = RegionLookup.FindRace(Races, r.DominantRace);
                var inf = r.Influence;
                sb.AppendLine($"=== {r.DisplayName}（{race.DisplayName}） ===");
                sb.AppendLine($"人口 {r.Population:0} (Δ{r.PopulationDelta:0.00}) | 承载力 {r.LastCarryingCapacity:0} | 稳定 {r.Stability:0.00} | 教育 {r.Education:0.00} | 信仰 {r.FaithLevel:0.00} | 疫病 {r.DiseasePressure:0.00}");
                sb.AppendLine($"粮 {r.Get(ResourceId.Food):0} (产能 {r.GetProductionCapacity(ResourceId.Food):0}, 日产 {r.LastFoodProduction:0.0}, 腐 {r.LastFoodSpoilage:0.0}, 储备 {r.LastFoodReserveDays:0.0}日)");
                sb.AppendLine($"水 {r.Get(ResourceId.Water):0}/{r.LastWaterCapacity:0} (供水系数 {r.LastWaterFactor:0.00}, 生活 {r.LastLivingWaterUsed:0.0}/农业 {r.LastAgriculturalWaterUsed:0.0})");
                sb.AppendLine($"木 {r.Get(ResourceId.Timber):0}  矿 {r.Get(ResourceId.Ore):0}  信资 {r.Get(ResourceId.Faith):0}  知识 {r.Get(ResourceId.Knowledge):0}  魔力 {r.Get(ResourceId.Magic):0}");
                sb.AppendLine($"天气 {r.WeatherFactor:0.00} | 地区注视 育×{inf.FertilityBlessing:0.00} 收×{inf.HarvestBlessing:0.00} 疫×{inf.DiseasePressure:0.00} 稳×{inf.StabilityBlessing:0.00}");
                sb.AppendLine($"近况: {r.LastEvent}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
