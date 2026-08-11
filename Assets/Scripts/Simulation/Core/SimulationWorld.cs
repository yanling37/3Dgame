using System;
using System.Text;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using DivineWorld.Simulation.Systems;
using DivineWorld.Simulation.Testing;
using UnityEngine;

namespace DivineWorld.Simulation.Core
{
    /// <summary>
    /// World host. Daily path: Season → Resource → Population → Events.
    /// Fast path: FastForwardSystem (no TickDay loop).
    /// </summary>
    public class SimulationWorld : MonoBehaviour
    {
        [SerializeField] int seed = 20260810;
        [SerializeField] float secondsPerDay = 0.35f;
        [SerializeField] bool autoRun = true;
        [SerializeField, Range(1, 30)] int daysPerFrameWhenFast = 1;

        public WorldState State { get; private set; }
        public RaceDefinition[] Races { get; private set; }
        public ObserverInfluence Influence { get; private set; } = new ObserverInfluence();
        public int Seed => seed;
        public string LastConsistencyReport { get; private set; } = "";

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
            if (!autoRun || State == null)
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
            Races = DefaultWorldFactory.CreateRaces();
            State = DefaultWorldFactory.CreateWorld();
            State.RandomSeed = seed;
            Influence = new ObserverInfluence();
            SeasonSystem.SyncFromCalendar(State);
            _dayTimer = 0f;
            LastConsistencyReport = "";
            OnDayAdvanced?.Invoke(State);
        }

        /// <summary>
        /// Daily order: SyncSeason → Resource → Population → Calendar → Events.
        /// </summary>
        public void AdvanceDay()
        {
            if (State == null)
            {
                return;
            }

            SeasonSystem.SyncFromCalendar(State);
            foreach (var region in State.Regions)
            {
                var race = RegionLookup.FindRace(Races, region.DominantRace);
                ResourceSystem.TickDay(region, race, Influence, State.CurrentSeason, _rng);
                PopulationSystem.TickDay(region, race, Influence, State.CurrentSeason, _rng);
            }

            State.DayOfYear++;
            State.TotalDays++;
            if (State.DayOfYear > WorldState.DaysPerYear)
            {
                State.DayOfYear = 1;
                State.Year++;
                EventSystem.ApplyYearTurn(State);
            }

            EventSystem.EvaluateAndApply(State, _rng);
            SeasonSystem.SyncFromCalendar(State);
            OnDayAdvanced?.Invoke(State);
        }

        public void AdvanceDays(int days)
        {
            for (int i = 0; i < days; i++)
            {
                AdvanceDay();
            }
        }

        /// <summary>
        /// Mathematical fast-forward. Replaces live State with projected result.
        /// </summary>
        public void FastForwardYears(int years)
        {
            if (State == null || years <= 0)
            {
                return;
            }

            bool wasAuto = autoRun;
            autoRun = false;
            var result = FastForwardSystem.FastForwardYears(State, Races, Influence, years, seed);
            State = result.State;
            SeasonSystem.SyncFromCalendar(State);
            Debug.Log(result.Log);
            OnDayAdvanced?.Invoke(State);
            autoRun = wasAuto;
        }

        public string RunConsistencyTestOneYear()
        {
            if (State == null)
            {
                return "世界未初始化";
            }

            var report = FastForwardConsistencyTest.RunOneYear(State, Races, Influence, seed);
            LastConsistencyReport = report.Text;
            return LastConsistencyReport;
        }

        public string BuildStatusReport()
        {
            if (State == null)
            {
                return "世界未初始化";
            }

            var sb = new StringBuilder(1536);
            sb.AppendLine($"【{State.WorldName}】 年份 {State.Year}  第 {State.DayOfYear} 日  (累计 {State.TotalDays} 日)");
            sb.AppendLine($"季节: {SeasonSystem.DisplayName(State.CurrentSeason)} ({State.CurrentSeason}) 进度 {State.SeasonProgress * 100f:0}%  Index={State.SeasonIndex}");
            sb.AppendLine($"注视焦点: {(Influence.FocusRegion.HasValue ? Influence.FocusRegion.Value.ToString() : "全域")}");
            sb.AppendLine($"微调: 生育×{Influence.FertilityBlessing:0.00} 收成×{Influence.HarvestBlessing:0.00} 疫病×{Influence.DiseaseCurse:0.00} 稳定×{Influence.StabilityBlessing:0.00}");
            sb.AppendLine();

            foreach (var r in State.Regions)
            {
                var race = RegionLookup.FindRace(Races, r.DominantRace);
                sb.AppendLine($"=== {r.DisplayName}（{race.DisplayName}） ===");
                sb.AppendLine($"人口 {r.Population:0} (Δ {r.PopulationDelta:+0.0;-0.0}) | 稳定 {r.Stability:0.00} | 教育 {r.Education:0.00} | 信仰 {r.FaithLevel:0.00} | 疫病 {r.DiseasePressure:0.00}");
                sb.AppendLine($"粮 {r.Get(ResourceId.Food):0}  水 {r.Get(ResourceId.Water):0}  木 {r.Get(ResourceId.Timber):0}  矿 {r.Get(ResourceId.Ore):0}");
                sb.AppendLine($"信资 {r.Get(ResourceId.Faith):0}  知识 {r.Get(ResourceId.Knowledge):0}  Mana {r.Get(ResourceId.Magic):0}");
                sb.AppendLine($"天气 {r.WeatherFactor:0.00} | 事件: {r.LastEvent}");
                sb.AppendLine();
            }

            if (!string.IsNullOrEmpty(LastConsistencyReport))
            {
                sb.AppendLine("--- 最近一致性测试 ---");
                sb.AppendLine(LastConsistencyReport);
            }

            return sb.ToString();
        }
    }
}
