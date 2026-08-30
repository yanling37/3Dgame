using DivineWorld.Simulation.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DivineWorld.Simulation.UI
{
    /// <summary>
    /// Always-on IMGUI chrome: calendar, time controls, and window tabs.
    /// Large panels are exclusive and deactivated with SetActive so they cannot eat clicks.
    /// </summary>
    public class PersistentHud : MonoBehaviour
    {
        [SerializeField] SimulationWorld world;

        readonly HudWindowState _windows = new HudWindowState(HudWindowId.Observer);
        WorldObserverHud _observer;
        HistoryTrendHud _history;
        PoliticsHud _politics;

        public HudWindowState Windows => _windows;

        public void Bind(
            SimulationWorld simulationWorld,
            WorldObserverHud observer,
            HistoryTrendHud history,
            PoliticsHud politics)
        {
            world = simulationWorld;
            _observer = observer;
            _history = history;
            _politics = politics;
            if (_observer != null)
            {
                _observer.BindWindows(_windows);
            }

            if (_history != null)
            {
                _history.BindWindows(_windows);
            }

            if (_politics != null)
            {
                _politics.BindWindows(_windows);
            }

            ApplyActive();
        }

        void LateUpdate()
        {
            ApplyActive();
        }

        void OnGUI()
        {
            if (world == null)
            {
                return;
            }

            GUI.depth = -10;
            var bar = ObservationHudLayout.PersistentBarRect(Screen.width);
            GUI.Box(bar, GUIContent.none);

            GUILayout.BeginArea(new Rect(bar.x + 8f, bar.y + 4f, bar.width - 16f, bar.height - 8f));

            GUILayout.BeginHorizontal();
            GUILayout.Label("打开面板", GUILayout.Width(64f), GUILayout.Height(32f));
            DrawTab("观察", HudWindowId.Observer);
            DrawTab("图表", HudWindowId.History);
            DrawTab("政治", HudWindowId.Politics);
            if (GUILayout.Button("进入降临吧", GUILayout.Width(96f), GUILayout.Height(32f)))
            {
                SceneManager.LoadScene("Jianglin");
            }
            if (_windows.OpenWindow == HudWindowId.None)
            {
                GUILayout.Label("（当前已全部隐藏，点左边按钮打开）");
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (world.State != null)
            {
                ObservationHudLayout.DrawCalendarClock(
                    world.CurrentYear,
                    world.CurrentSeason,
                    world.DayOfYear,
                    world.DayInSeason);
            }

            DrawTimeButtons();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        void DrawTimeButtons()
        {
            GUILayout.BeginVertical(GUILayout.Width(320f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(world.AutoRun ? "暂停" : "继续", GUILayout.Width(54f), GUILayout.Height(24f)))
            {
                world.AutoRun = !world.AutoRun;
            }

            if (GUILayout.Button("+1日", GUILayout.Width(48f), GUILayout.Height(24f)))
            {
                world.AdvanceDay();
            }

            if (GUILayout.Button("+30日", GUILayout.Width(54f), GUILayout.Height(24f)))
            {
                world.AdvanceDays(30);
            }

            if (GUILayout.Button("+1年", GUILayout.Width(48f), GUILayout.Height(24f)))
            {
                world.FastForwardYears(1);
            }

            if (GUILayout.Button("重置", GUILayout.Width(48f), GUILayout.Height(24f)))
            {
                world.ResetWorld();
            }

            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        void DrawTab(string label, HudWindowId id)
        {
            bool on = _windows.IsOpen(id);
            var prev = GUI.backgroundColor;
            if (on)
            {
                GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
            }

            string text = on ? "隐藏" + label : "打开" + label;
            if (GUILayout.Button(text, GUILayout.Width(88f), GUILayout.Height(32f)))
            {
                _windows.Toggle(id);
                ApplyActive();
            }

            GUI.backgroundColor = prev;
        }

        void ApplyActive()
        {
            SetWindowActive(_observer, _windows.IsOpen(HudWindowId.Observer));
            SetWindowActive(_history, _windows.IsOpen(HudWindowId.History));
            SetWindowActive(_politics, _windows.IsOpen(HudWindowId.Politics));
        }

        static void SetWindowActive(MonoBehaviour hud, bool on)
        {
            if (hud == null)
            {
                return;
            }

            if (hud.gameObject.activeSelf != on)
            {
                hud.gameObject.SetActive(on);
            }
        }
    }
}
