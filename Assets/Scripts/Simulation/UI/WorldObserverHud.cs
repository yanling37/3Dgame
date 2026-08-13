using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;
using UnityEngine;

namespace DivineWorld.Simulation.UI
{
    /// <summary>
    /// Observer HUD: region information panel plus P2-B observation snapshot.
    /// </summary>
    public class WorldObserverHud : MonoBehaviour
    {
        [SerializeField] SimulationWorld world;
        [SerializeField] ObservationHost observation;
        [SerializeField] bool visible = true;

        Vector2 _scroll;
        string _cachedReport = "";
        int _lastDay = -1;
        bool _subscribed;
        bool _observationSubscribed;

        public void Bind(SimulationWorld simulationWorld)
        {
            Bind(simulationWorld, observation);
        }

        public void Bind(SimulationWorld simulationWorld, ObservationHost observationHost)
        {
            if (world != null && _subscribed)
            {
                world.OnDayAdvanced -= OnDay;
                _subscribed = false;
            }

            if (observation != null && _observationSubscribed)
            {
                observation.OnSnapshotUpdated -= OnSnapshot;
                _observationSubscribed = false;
            }

            world = simulationWorld;
            observation = observationHost;
            if (world != null)
            {
                world.OnDayAdvanced += OnDay;
                _subscribed = true;
            }

            if (observation != null)
            {
                observation.OnSnapshotUpdated += OnSnapshot;
                _observationSubscribed = true;
            }

            Refresh();
        }

        void OnDay(WorldState _)
        {
            Refresh();
        }

        void OnSnapshot(WorldObservationSnapshot _)
        {
            Refresh();
        }

        void Start()
        {
            if (world == null)
            {
                world = FindObjectOfType<SimulationWorld>();
            }

            if (observation == null)
            {
                observation = FindObjectOfType<ObservationHost>();
            }

            if (world != null && !_subscribed)
            {
                Bind(world, observation);
            }
        }

        void OnDestroy()
        {
            if (world != null && _subscribed)
            {
                world.OnDayAdvanced -= OnDay;
                _subscribed = false;
            }

            if (observation != null && _observationSubscribed)
            {
                observation.OnSnapshotUpdated -= OnSnapshot;
                _observationSubscribed = false;
            }
        }

        void Refresh()
        {
            if (world == null)
            {
                return;
            }

            world.Influence?.PullFromFocus();
            _cachedReport = world.BuildStatusReport();
            _lastDay = world.State != null ? world.State.TotalDays : -1;
        }

        void OnGUI()
        {
            if (!visible || world == null)
            {
                return;
            }

            const float pad = 12f;
            var area = new Rect(pad, pad, Mathf.Min(560f, Screen.width - pad * 2f), Screen.height - pad * 2f);
            GUI.Box(area, ObservationVersion.HudTitle);

            GUILayout.BeginArea(new Rect(area.x + 10, area.y + 28, area.width - 20, area.height - 36));
            GUILayout.Label(ObservationVersion.HudTitle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(world.AutoRun ? "暂停" : "继续", GUILayout.Width(70)))
            {
                world.AutoRun = !world.AutoRun;
            }

            if (GUILayout.Button("+1日", GUILayout.Width(55)))
            {
                world.AdvanceDay();
            }

            if (GUILayout.Button("+30日", GUILayout.Width(60)))
            {
                world.AdvanceDays(30);
            }

            if (GUILayout.Button("+1年快进", GUILayout.Width(80)))
            {
                world.FastForwardYears(1);
            }

            if (GUILayout.Button("+10年快进", GUILayout.Width(90)))
            {
                world.FastForwardYears(10);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+360日", GUILayout.Width(80)))
            {
                world.AdvanceDays(360);
                Refresh();
            }

            if (GUILayout.Button("快进1年", GUILayout.Width(80)))
            {
                world.FastForwardYears(1);
                Refresh();
            }

            if (GUILayout.Button("重置世界", GUILayout.Width(80)))
            {
                world.ResetWorld();
            }

            if (GUILayout.Button("一致性测试 1年", GUILayout.Width(120)))
            {
                world.RunConsistencyTestOneYear();
                Refresh();
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("一致性 Daily vs Fast 1年", GUILayout.Height(28)))
            {
                var report = DivineWorld.Simulation.Testing.FastForwardConsistencyTest.Run(
                    world.State.Clone(),
                    world.Races,
                    world.Config,
                    360);
                Debug.Log(report.Text);
                _cachedReport = report.Text + "\n\n" + world.BuildStatusReport();
            }

            GUILayout.EndHorizontal();

            if (world.State != null)
            {
                GUILayout.Label($"季节 {world.CurrentSeason} | 年 {world.CurrentYear} | 第 {world.DayOfYear} 日 | 季内 {world.DayInSeason}/90");
            }

            DrawRegionObservationPanel();

            GUILayout.Space(6);
            GUILayout.Label("速度（秒/日）");
            world.SecondsPerDay = GUILayout.HorizontalSlider(world.SecondsPerDay, 0.05f, 1.5f);

            GUILayout.Space(8);
            GUILayout.Label("注视地区（微调写入该地区的独立 Influence）");
            GUILayout.BeginHorizontal();
            FocusBtn("全域", null);
            FocusBtn("教廷区", RegionId.Theocracy);
            FocusBtn("帝国区", RegionId.Empire);
            FocusBtn("海", RegionId.Sea);
            GUILayout.EndHorizontal();

            var inf = world.Influence;
            GUILayout.Space(6);
            GUILayout.Label($"生育祝福 ×{inf.FertilityBlessing:0.00}");
            float fert = GUILayout.HorizontalSlider(inf.FertilityBlessing, 0.7f, 1.3f);
            GUILayout.Label($"收成祝福 ×{inf.HarvestBlessing:0.00}");
            float harvest = GUILayout.HorizontalSlider(inf.HarvestBlessing, 0.7f, 1.3f);
            GUILayout.Label($"疫病压力 ×{inf.DiseaseCurse:0.00}");
            float disease = GUILayout.HorizontalSlider(inf.DiseaseCurse, 0.7f, 1.3f);
            GUILayout.Label($"稳定祝福 ×{inf.StabilityBlessing:0.00}");
            float stability = GUILayout.HorizontalSlider(inf.StabilityBlessing, 0.7f, 1.3f);

            if (!Mathf.Approximately(fert, inf.FertilityBlessing)
                || !Mathf.Approximately(harvest, inf.HarvestBlessing)
                || !Mathf.Approximately(disease, inf.DiseaseCurse)
                || !Mathf.Approximately(stability, inf.StabilityBlessing))
            {
                inf.FertilityBlessing = fert;
                inf.HarvestBlessing = harvest;
                inf.DiseaseCurse = disease;
                inf.StabilityBlessing = stability;
                inf.PushToFocus();
            }

            if (GUILayout.Button("清除微调", GUILayout.Width(100)))
            {
                inf.ResetSoft();
            }

            GUILayout.Space(8);
            if (world.State != null && world.State.TotalDays != _lastDay)
            {
                Refresh();
            }

            _scroll = GUILayout.BeginScrollView(_scroll);
            GUILayout.TextArea(_cachedReport, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        void FocusBtn(string label, RegionId? region)
        {
            bool on = world.Influence.FocusRegion == region;
            var prev = GUI.backgroundColor;
            if (on)
            {
                GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
            }

            if (GUILayout.Button(label, GUILayout.Height(28)))
            {
                world.Influence.PushToFocus();
                world.Influence.FocusRegion = region;
                world.Influence.PullFromFocus();
            }

            GUI.backgroundColor = prev;
        }

        void DrawRegionObservationPanel()
        {
            GUILayout.Space(8);
            GUILayout.Label("地区观察");
            var snap = observation != null ? observation.Current : null;
            if (snap == null || snap.Regions == null || snap.Regions.Length == 0)
            {
                GUILayout.Label("（等待观察快照）");
                return;
            }

            var cfg = PopulationVisualizationConfig.CreateDefault();
            GUILayout.Label($"快照 年 {snap.Year}  第 {snap.DayOfYear} 日  累计 {snap.TotalDays}  {snap.Season}");
            for (int i = 0; i < snap.Regions.Length; i++)
            {
                var r = snap.Regions[i];
                if (r == null)
                {
                    continue;
                }

                int markers = PopulationMarkerRules.MarkerCount(r.Population, cfg);
                GUILayout.Label(
                    $"{r.DisplayName}  人口 {r.Population:0}  (Δ{r.PopulationDelta:0.00})  标记 {markers}/{cfg.MaxMarkersPerRegion}  粮 {r.Food:0}  水 {r.Water:0}  稳定 {r.Stability:0.00}");
            }
        }
    }
}
