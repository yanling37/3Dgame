using System;
using System.Text;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using DivineWorld.Simulation.Systems;
using UnityEngine;

namespace DivineWorld.Simulation.Core
{
    /// <summary>
    /// 世界模拟宿主：持有 WorldState / 种族表 / 观察者微调，并按日推进。
    /// 调试建议：
    /// 1) 固定 seed，保证随机天气/事件可复现；
    /// 2) 把 autoRun 关掉，用 HUD 的 +1日 / +30日 单步进；
    /// 3) 在 AdvanceDay 里对某个 Region 下断点，再步入 ResourceSystem / PopulationSystem。
    /// </summary>
    public class SimulationWorld : MonoBehaviour
    {
        [Tooltip("世界随机种子。改这个会改变天气漂移与事件触发序列。")]
        [SerializeField] int seed = 20260810;

        [Tooltip("自动运行时，多少真实秒推进 1 个游戏日。")]
        [SerializeField] float secondsPerDay = 0.35f;

        [Tooltip("是否自动按时间推进。调试时建议先关掉，改用手动 +日。")]
        [SerializeField] bool autoRun = true;

        [Tooltip("每次满足一日计时时，连续推进几天（加速用）。调试逐步逻辑时保持 1。")]
        [SerializeField, Range(1, 30)] int daysPerFrameWhenFast = 1;

        public WorldState State { get; private set; }
        public RaceDefinition[] Races { get; private set; }
        public ObserverInfluence Influence { get; private set; } = new ObserverInfluence();

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

        /// <summary>每推进完一日（所有地区都结算后）触发，HUD / 图腾会监听它刷新。</summary>
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

        /// <summary>
        /// 重建初始世界。会重置 Influence 微调为 1.0，并重新用 seed 创建 Random。
        /// </summary>
        public void ResetWorld()
        {
            _rng = new System.Random(seed);
            Races = DefaultWorldFactory.CreateRaces();
            State = DefaultWorldFactory.CreateWorld();
            Influence = new ObserverInfluence();
            _dayTimer = 0f;
            OnDayAdvanced?.Invoke(State);
        }

        /// <summary>
        /// 推进完整一日。顺序固定，调试时不要对调：
        /// 对每个地区：ResourceSystem.TickDay → PopulationSystem.TickDay
        /// 然后：日期 +1；满 360 日进一年。
        /// </summary>
        public void AdvanceDay()
        {
            if (State == null)
            {
                return;
            }

            foreach (var region in State.Regions)
            {
                var race = RegionLookup.FindRace(Races, region.DominantRace);

                // 1) 先结算资源（产量/消耗/缺粮反馈/天气）
                ResourceSystem.TickDay(region, race, Influence, _rng);

                // 2) 再结算人口（出生死亡会读到「本轮更新后」的粮库存）
                PopulationSystem.TickDay(region, race, Influence, _rng);
            }

            State.DayOfYear++;
            State.TotalDays++;
            if (State.DayOfYear > 360)
            {
                State.DayOfYear = 1;
                State.Year++;
                foreach (var region in State.Regions)
                {
                    region.LastEvent = $"新年纪事 · {State.Year}";
                }
            }

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
        /// 给观察仪用的文本快照。数值格式化集中在这里，改显示不必动算法。
        /// </summary>
        public string BuildStatusReport()
        {
            if (State == null)
            {
                return "世界未初始化";
            }

            var sb = new StringBuilder(1024);
            sb.AppendLine($"【{State.WorldName}】 年份 {State.Year}  第 {State.DayOfYear} 日  (累计 {State.TotalDays} 日)");
            sb.AppendLine($"注视焦点: {(Influence.FocusRegion.HasValue ? Influence.FocusRegion.Value.ToString() : "全域")}");
            sb.AppendLine($"微调: 生育×{Influence.FertilityBlessing:0.00} 收成×{Influence.HarvestBlessing:0.00} 疫病×{Influence.DiseaseCurse:0.00} 稳定×{Influence.StabilityBlessing:0.00}");
            sb.AppendLine();

            foreach (var r in State.Regions)
            {
                var race = RegionLookup.FindRace(Races, r.DominantRace);
                sb.AppendLine($"=== {r.DisplayName}（{race.DisplayName}） ===");
                sb.AppendLine($"人口 {r.Population:0} | 稳定 {r.Stability:0.00} | 教育 {r.Education:0.00} | 信仰 {r.FaithLevel:0.00} | 疫病 {r.DiseasePressure:0.00}");
                sb.AppendLine($"粮 {r.Get(ResourceId.Food):0}  水 {r.Get(ResourceId.Water):0}  木 {r.Get(ResourceId.Timber):0}  矿 {r.Get(ResourceId.Ore):0}");
                sb.AppendLine($"信资 {r.Get(ResourceId.Faith):0}  知识 {r.Get(ResourceId.Knowledge):0}  魔力 {r.Get(ResourceId.Magic):0}");
                sb.AppendLine($"天气系数 {r.WeatherFactor:0.00} | 近况: {r.LastEvent}");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}
