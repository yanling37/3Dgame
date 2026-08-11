using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Save;
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
        string _statusMessage = "";
        float _statusUntil;
        bool _bound;

        public void Bind(SimulationWorld simulationWorld)
        {
            if (world != null && _bound)
            {
                world.OnDayAdvanced -= OnDayAdvanced;
            }

            world = simulationWorld;
            _bound = false;
            if (world != null)
            {
                world.OnDayAdvanced += OnDayAdvanced;
                _bound = true;
                Refresh();
                RefreshSlotHints();
            }
        }

        void Start()
        {
            if (world == null)
            {
                world = FindObjectOfType<SimulationWorld>();
            }

            if (world != null && !_bound)
            {
                world.OnDayAdvanced += OnDayAdvanced;
                _bound = true;
                Refresh();
                RefreshSlotHints();
            }
        }

        void OnDestroy()
        {
            if (world != null && _bound)
            {
                world.OnDayAdvanced -= OnDayAdvanced;
                _bound = false;
            }
        }

        void OnDayAdvanced(WorldState state)
        {
            Refresh();
            if (state != null && state.TotalDays > 0 && state.TotalDays % 30 == 0)
            {
                TryAutosave("自动存档（每30日）");
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

        readonly string[] _slotHints = new string[4];

        void RefreshSlotHints()
        {
            for (int i = 0; i <= 3; i++)
            {
                var slot = (SaveSlot)i;
                if (SaveService.TryGetSlotInfo(slot, out var info) && info.Exists)
                {
                    _slotHints[i] = $"{SaveService.SlotLabel(slot)}: 年{info.Year} / {info.TotalDays}日";
                }
                else
                {
                    _slotHints[i] = $"{SaveService.SlotLabel(slot)}: 空";
                }
            }
        }

        void SetStatus(string message)
        {
            _statusMessage = message;
            _statusUntil = Time.realtimeSinceStartup + 3.5f;
        }

        void TryAutosave(string reason)
        {
            if (world == null)
            {
                return;
            }

            if (SaveService.TrySave(SaveSlot.Autosave, world.ToSaveDto(), out var error))
            {
                SetStatus($"{reason} 成功");
                RefreshSlotHints();
            }
            else
            {
                SetStatus($"自动存档失败: {error}");
            }
        }

        void SaveSlot(SaveSlot slot)
        {
            if (world == null)
            {
                return;
            }

            if (SaveService.TrySave(slot, world.ToSaveDto(), out var error))
            {
                SetStatus($"已存入 {SaveService.SlotLabel(slot)}");
                RefreshSlotHints();
            }
            else
            {
                SetStatus($"存档失败: {error}");
            }
        }

        void LoadSlot(SaveSlot slot)
        {
            if (world == null)
            {
                return;
            }

            if (!SaveService.TryLoad(slot, out var dto, out var error))
            {
                SetStatus($"读档失败: {error}");
                return;
            }

            if (!world.ApplySaveDto(dto, out error))
            {
                SetStatus($"读档失败: {error}");
                return;
            }

            SetStatus($"已读取 {SaveService.SlotLabel(slot)}");
            Refresh();
            RefreshSlotHints();
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
                bool wasRunning = world.AutoRun;
                world.AutoRun = !world.AutoRun;
                if (wasRunning && !world.AutoRun)
                {
                    TryAutosave("暂停自动存档");
                }
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
            GUILayout.Label("存档");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("存1", GUILayout.Width(50)))
            {
                SaveSlot(SaveSlot.Slot1);
            }

            if (GUILayout.Button("存2", GUILayout.Width(50)))
            {
                SaveSlot(SaveSlot.Slot2);
            }

            if (GUILayout.Button("存3", GUILayout.Width(50)))
            {
                SaveSlot(SaveSlot.Slot3);
            }

            if (GUILayout.Button("存自动", GUILayout.Width(60)))
            {
                SaveSlot(SaveSlot.Autosave);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("读1", GUILayout.Width(50)))
            {
                LoadSlot(SaveSlot.Slot1);
            }

            if (GUILayout.Button("读2", GUILayout.Width(50)))
            {
                LoadSlot(SaveSlot.Slot2);
            }

            if (GUILayout.Button("读3", GUILayout.Width(50)))
            {
                LoadSlot(SaveSlot.Slot3);
            }

            if (GUILayout.Button("读自动", GUILayout.Width(60)))
            {
                LoadSlot(SaveSlot.Autosave);
            }

            GUILayout.EndHorizontal();

            GUILayout.Label(_slotHints[1] ?? "槽1: ?");
            GUILayout.Label(_slotHints[2] ?? "槽2: ?");
            GUILayout.Label(_slotHints[3] ?? "槽3: ?");
            GUILayout.Label(_slotHints[0] ?? "自动: ?");

            if (!string.IsNullOrEmpty(_statusMessage) && Time.realtimeSinceStartup < _statusUntil)
            {
                GUILayout.Label(_statusMessage);
            }

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
