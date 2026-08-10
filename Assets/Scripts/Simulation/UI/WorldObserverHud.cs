using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.UI
{
    /// <summary>
    /// Immediate-mode observer panel so Phase 1 runs without manual Canvas wiring.
    /// </summary>
    public class WorldObserverHud : MonoBehaviour
    {
        [SerializeField] SimulationWorld world;
        [SerializeField] bool visible = true;

        Vector2 _scroll;
        string _cachedReport = "";
        int _lastDay = -1;

        public void Bind(SimulationWorld simulationWorld)
        {
            world = simulationWorld;
            if (world != null)
            {
                world.OnDayAdvanced += _ => Refresh();
                Refresh();
            }
        }

        void Start()
        {
            if (world == null)
            {
                world = FindObjectOfType<SimulationWorld>();
            }

            if (world != null)
            {
                world.OnDayAdvanced += _ => Refresh();
                Refresh();
            }
        }

        void Refresh()
        {
            if (world == null)
            {
                return;
            }

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
            var area = new Rect(pad, pad, Mathf.Min(520f, Screen.width - pad * 2f), Screen.height - pad * 2f);
            GUI.Box(area, "Divine World · 观察仪 (Phase 1)");

            GUILayout.BeginArea(new Rect(area.x + 10, area.y + 28, area.width - 20, area.height - 36));

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(world.AutoRun ? "暂停" : "继续", GUILayout.Width(70)))
            {
                world.AutoRun = !world.AutoRun;
            }

            if (GUILayout.Button("+1日", GUILayout.Width(60)))
            {
                world.AdvanceDay();
                Refresh();
            }

            if (GUILayout.Button("+30日", GUILayout.Width(70)))
            {
                world.AdvanceDays(30);
                Refresh();
            }

            if (GUILayout.Button("重置世界", GUILayout.Width(80)))
            {
                world.ResetWorld();
                Refresh();
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("速度（秒/日）");
            world.SecondsPerDay = GUILayout.HorizontalSlider(world.SecondsPerDay, 0.05f, 1.5f);

            GUILayout.Space(8);
            GUILayout.Label("注视地区（微调主要作用于此）");
            GUILayout.BeginHorizontal();
            FocusBtn("全域", null);
            FocusBtn("教廷区", RegionId.Theocracy);
            FocusBtn("帝国区", RegionId.Empire);
            FocusBtn("海", RegionId.Sea);
            GUILayout.EndHorizontal();

            var inf = world.Influence;
            GUILayout.Space(6);
            GUILayout.Label($"生育祝福 ×{inf.FertilityBlessing:0.00}");
            inf.FertilityBlessing = GUILayout.HorizontalSlider(inf.FertilityBlessing, 0.7f, 1.3f);
            GUILayout.Label($"收成祝福 ×{inf.HarvestBlessing:0.00}");
            inf.HarvestBlessing = GUILayout.HorizontalSlider(inf.HarvestBlessing, 0.7f, 1.3f);
            GUILayout.Label($"疫病压力 ×{inf.DiseaseCurse:0.00}");
            inf.DiseaseCurse = GUILayout.HorizontalSlider(inf.DiseaseCurse, 0.7f, 1.3f);
            GUILayout.Label($"稳定祝福 ×{inf.StabilityBlessing:0.00}");
            inf.StabilityBlessing = GUILayout.HorizontalSlider(inf.StabilityBlessing, 0.7f, 1.3f);

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
                world.Influence.FocusRegion = region;
            }

            GUI.backgroundColor = prev;
        }
    }
}
