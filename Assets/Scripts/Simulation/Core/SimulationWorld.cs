using System;
using System.Text;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using DivineWorld.Simulation.Save;
using DivineWorld.Simulation.Systems;
using UnityEngine;

namespace DivineWorld.Simulation.Core
{
    /// <summary>
    /// Owns world state and advances simulation ticks.
    /// </summary>
    public class SimulationWorld : MonoBehaviour
    {
        [SerializeField] int seed = 20260810;
        [SerializeField] float secondsPerDay = 0.35f;
        [SerializeField] bool autoRun = true;
        [SerializeField, Range(1, 30)] int daysPerFrameWhenFast = 1;

        public int Seed => seed;
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
            Influence = new ObserverInfluence();
            _dayTimer = 0f;
            OnDayAdvanced?.Invoke(State);
        }

        public SaveGameDto ToSaveDto()
        {
            // Round-trip clone so the DTO does not alias live mutable state.
            string worldJson = State != null ? JsonUtility.ToJson(State) : null;
            var worldClone = string.IsNullOrEmpty(worldJson)
                ? null
                : JsonUtility.FromJson<WorldState>(worldJson);

            var dto = new SaveGameDto
            {
                schemaVersion = SaveService.CurrentSchemaVersion,
                seed = seed,
                secondsPerDay = secondsPerDay,
                autoRun = autoRun,
                world = worldClone,
                fertilityBlessing = Influence.FertilityBlessing,
                harvestBlessing = Influence.HarvestBlessing,
                diseaseCurse = Influence.DiseaseCurse,
                stabilityBlessing = Influence.StabilityBlessing,
                hasFocusRegion = Influence.FocusRegion.HasValue,
                focusRegion = Influence.FocusRegion ?? RegionId.Theocracy
            };
            return dto;
        }

        public bool ApplySaveDto(SaveGameDto dto, out string error)
        {
            error = null;
            if (dto == null || dto.world == null)
            {
                error = "存档数据无效";
                return false;
            }

            if (dto.schemaVersion != SaveService.CurrentSchemaVersion)
            {
                error = $"存档版本不兼容（文件 v{dto.schemaVersion}，当前 v{SaveService.CurrentSchemaVersion}）";
                return false;
            }

            seed = dto.seed;
            secondsPerDay = Mathf.Max(0.05f, dto.secondsPerDay);
            autoRun = dto.autoRun;

            string worldJson = JsonUtility.ToJson(dto.world);
            State = JsonUtility.FromJson<WorldState>(worldJson);
            EnsureRegionResources(State);

            Races = DefaultWorldFactory.CreateRaces();
            Influence = new ObserverInfluence
            {
                FertilityBlessing = dto.fertilityBlessing,
                HarvestBlessing = dto.harvestBlessing,
                DiseaseCurse = dto.diseaseCurse,
                StabilityBlessing = dto.stabilityBlessing,
                FocusRegion = dto.hasFocusRegion ? dto.focusRegion : (RegionId?)null
            };

            int totalDays = State != null ? State.TotalDays : 0;
            _rng = new System.Random(unchecked(seed ^ (totalDays * 397)));
            _dayTimer = 0f;
            OnDayAdvanced?.Invoke(State);
            return true;
        }

        static void EnsureRegionResources(WorldState world)
        {
            if (world?.Regions == null)
            {
                return;
            }

            foreach (var region in world.Regions)
            {
                if (region == null)
                {
                    continue;
                }

                if (region.Resources == null || region.Resources.Length < 7)
                {
                    var resized = new float[7];
                    if (region.Resources != null)
                    {
                        Array.Copy(region.Resources, resized, Math.Min(region.Resources.Length, 7));
                    }

                    region.Resources = resized;
                }
            }
        }

        public void AdvanceDay()
        {
            if (State == null)
            {
                return;
            }

            foreach (var region in State.Regions)
            {
                var race = RegionLookup.FindRace(Races, region.DominantRace);
                ResourceSystem.TickDay(region, race, Influence, _rng);
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
