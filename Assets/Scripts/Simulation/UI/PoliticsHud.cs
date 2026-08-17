using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Politics;
using UnityEngine;

namespace DivineWorld.Simulation.UI
{
    /// <summary>
    /// P2-C v0.2 Politics panel: undirected relations with directed diplomatic actions.
    /// Debug / test buttons only call PoliticsSystem; they do not write population or resources.
    /// </summary>
    public class PoliticsHud : MonoBehaviour
    {
        [SerializeField] SimulationWorld world;
        [SerializeField] bool visible = true;

        HudWindowState _windows;
        Vector2 _scroll;
        int _selectedPair;
        bool[] _sourceIsFirst = { true, true, true };
        string _reason = DiplomaticAction.DefaultImproveReason;
        PoliticsState _boundPolitics;

        public void Bind(SimulationWorld simulationWorld)
        {
            world = simulationWorld;
        }

        public void BindWindows(HudWindowState windows)
        {
            _windows = windows;
        }

        void Start()
        {
            if (world == null)
            {
                world = FindObjectOfType<SimulationWorld>();
            }
        }

        void OnGUI()
        {
            if (!visible || world == null || world.State == null)
            {
                return;
            }

            if (_windows != null && !_windows.IsOpen(HudWindowId.Politics))
            {
                return;
            }

            PoliticsSystem.EnsureInitialized(world.State);
            ResetUiIfPoliticsReplaced();

            var area = ObservationHudLayout.PoliticsPanel(Screen.width, Screen.height);
            GUI.Box(area, GUIContent.none);

            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 6f, area.width - 20f, area.height - 12f));
            GUILayout.BeginHorizontal();
            GUILayout.Label(PoliticsVersion.HudTitle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("隐藏", GUILayout.Width(56f), GUILayout.Height(22f)))
            {
                if (_windows != null)
                {
                    _windows.Close();
                }
                else
                {
                    visible = false;
                }
            }

            GUILayout.EndHorizontal();
            GUILayout.Label("Diplomatic Actions  (undirected pair · directed Source → Target · no war · no trade)");
            GUILayout.Label("War: " + WarReservation.Status + "    Peace: " + PeaceReservation.Status);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Reason", GUILayout.Width(52f));
            _reason = GUILayout.TextField(_reason ?? "", GUILayout.MinWidth(160f), GUILayout.Height(22f));
            GUILayout.EndHorizontal();

            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawPair(0, RegionId.Theocracy, RegionId.Empire);
            DrawPair(1, RegionId.Theocracy, RegionId.Sea);
            DrawPair(2, RegionId.Empire, RegionId.Sea);
            DrawTreaties();
            DrawDiplomaticHistory();
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        void ResetUiIfPoliticsReplaced()
        {
            var politics = world.State.Politics;
            if (ReferenceEquals(_boundPolitics, politics))
            {
                return;
            }

            _boundPolitics = politics;
            _selectedPair = 0;
            _sourceIsFirst = new[] { true, true, true };
            _reason = DiplomaticAction.DefaultImproveReason;
            _scroll = Vector2.zero;
        }

        void DrawPair(int index, RegionId a, RegionId b)
        {
            var relation = world.State.Politics != null ? world.State.Politics.FindRelation(a, b) : null;
            GUILayout.BeginVertical(GUI.skin.box);

            bool selected = _selectedPair == index;
            string pairTitle = a + " ↔ " + b;
            if (GUILayout.Button(selected ? "● " + pairTitle : pairTitle, GUILayout.Height(22f)))
            {
                _selectedPair = index;
            }

            if (relation == null)
            {
                GUILayout.Label("(missing relation)");
                GUILayout.EndVertical();
                return;
            }

            string signed = FormatSigned(relation.RelationValue);
            GUILayout.Label("Current Relation  " + signed);
            GUILayout.Label("Current State  " + relation.RelationState);
            GUILayout.Label("LastChangedDay " + relation.LastChangedDay);

            if (_sourceIsFirst.Length != 3)
            {
                _sourceIsFirst = new[] { true, true, true };
            }

            RegionId source = _sourceIsFirst[index] ? a : b;
            RegionId target = _sourceIsFirst[index] ? b : a;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Action:", GUILayout.Width(48f));
            if (GUILayout.Button(source + " → " + target, GUILayout.Height(22f)))
            {
                _sourceIsFirst[index] = !_sourceIsFirst[index];
            }

            GUILayout.EndHorizontal();

            float step = world.State.Politics.Config != null
                ? world.State.Politics.Config.DebugAdjustmentMagnitude
                : 10f;
            string reason = string.IsNullOrEmpty(_reason) ? DiplomaticAction.DefaultImproveReason : _reason;

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("改善关系", GUILayout.Width(88f), GUILayout.Height(24f)))
            {
                world.ImproveRelations(source, target, step, reason);
            }

            if (GUILayout.Button("恶化关系", GUILayout.Width(88f), GUILayout.Height(24f)))
            {
                world.WorsenRelations(source, target, step, reason);
            }

            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("外交事件", GUILayout.Width(88f), GUILayout.Height(22f)))
            {
                world.ApplyDiplomaticIncident(new DiplomaticIncident
                {
                    Type = DiplomaticIncidentType.BorderTension,
                    SourceRegion = source,
                    TargetRegion = target,
                    Delta = -step,
                    Reason = reason
                });
            }

            if (GUILayout.Button("条约占位", GUILayout.Width(88f), GUILayout.Height(22f)))
            {
                world.CreateTreaty(TreatyType.NonAggression, source, target, 90, reason);
            }

            GUILayout.EndHorizontal();

            var history = relation.History;
            if (history != null && history.Count > 0)
            {
                int start = history.Count > 5 ? history.Count - 5 : 0;
                for (int i = start; i < history.Count; i++)
                {
                    GUILayout.Label(history[i].ToObservationLine());
                }
            }

            GUILayout.EndVertical();
        }

        void DrawTreaties()
        {
            var politics = world.State.Politics;
            if (politics == null || politics.Treaties == null || politics.Treaties.Count == 0)
            {
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Treaties (placeholder · no trade · no military)");
            int day = world.State.TotalDays;
            for (int i = 0; i < politics.Treaties.Count; i++)
            {
                var treaty = politics.Treaties[i];
                if (treaty == null)
                {
                    continue;
                }

                string life = treaty.EndDay < 0 ? "open" : ("End " + treaty.EndDay);
                string active = treaty.IsActiveAt(day) ? "Active" : "Expired";
                GUILayout.Label(treaty.TreatyType + "  " + treaty.SourceRegion + " → " + treaty.TargetRegion
                    + "  Day " + treaty.StartDay + "–" + life + "  " + active);
            }

            GUILayout.EndVertical();
        }

        void DrawDiplomaticHistory()
        {
            var politics = world.State.Politics;
            if (politics == null || politics.DiplomaticHistory == null || politics.DiplomaticHistory.Count == 0)
            {
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("Diplomatic History");
            var history = politics.DiplomaticHistory;
            int start = history.Count > 8 ? history.Count - 8 : 0;
            for (int i = start; i < history.Count; i++)
            {
                GUILayout.Label(history[i].ToObservationLine());
            }

            GUILayout.EndVertical();
        }

        static string FormatSigned(float value)
        {
            string body = value.ToString("0.##");
            if (value > 0f && body.Length > 0 && body[0] != '+')
            {
                return "+" + body;
            }

            return body;
        }
    }
}
