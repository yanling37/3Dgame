using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Politics;
using UnityEngine;

namespace DivineWorld.Simulation.UI
{
    /// <summary>
    /// P2-C v0.1 Politics / Relations observation panel plus debug +/-10.
    /// Debug buttons only call PoliticsSystem; they do not write population or resources.
    /// </summary>
    public class PoliticsHud : MonoBehaviour
    {
        [SerializeField] SimulationWorld world;
        [SerializeField] bool visible = true;

        Vector2 _scroll;

        public void Bind(SimulationWorld simulationWorld)
        {
            world = simulationWorld;
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

            PoliticsSystem.EnsureInitialized(world.State);
            var area = ObservationHudLayout.PoliticsPanel(Screen.width, Screen.height);
            GUI.Box(area, GUIContent.none);

            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 6f, area.width - 20f, area.height - 12f));
            GUILayout.Label(PoliticsVersion.HudTitle);
            GUILayout.Label("Politics / Relations  (undirected · no war simulation)");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Regions:");
            GUILayout.Label("Theocracy");
            GUILayout.Label("Empire");
            GUILayout.Label("Sea");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Label("War: " + WarReservation.Status);
            GUILayout.Label("Debug / Test (observation layer only — does not change population or resources)");

            _scroll = GUILayout.BeginScrollView(_scroll);
            DrawPair(RegionId.Theocracy, RegionId.Empire);
            DrawPair(RegionId.Theocracy, RegionId.Sea);
            DrawPair(RegionId.Empire, RegionId.Sea);
            GUILayout.EndScrollView();

            GUILayout.EndArea();
        }

        void DrawPair(RegionId a, RegionId b)
        {
            var relation = world.State.Politics != null ? world.State.Politics.FindRelation(a, b) : null;
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(a + " ↔ " + b);
            if (relation == null)
            {
                GUILayout.Label("(missing relation)");
                GUILayout.EndVertical();
                return;
            }

            string signed = relation.RelationValue > 0f
                ? "+" + relation.RelationValue.ToString("0.##")
                : relation.RelationValue.ToString("0.##");
            GUILayout.Label(signed + "    " + relation.RelationState);
            GUILayout.Label("LastChangedDay " + relation.LastChangedDay);

            GUILayout.BeginHorizontal();
            float step = world.State.Politics.Config != null
                ? world.State.Politics.Config.DebugAdjustmentMagnitude
                : 10f;
            if (GUILayout.Button("Relation +" + step.ToString("0"), GUILayout.Width(110f), GUILayout.Height(22f)))
            {
                world.DebugAdjustRelation(a, b, step);
            }

            if (GUILayout.Button("Relation -" + step.ToString("0"), GUILayout.Width(110f), GUILayout.Height(22f)))
            {
                world.DebugAdjustRelation(a, b, -step);
            }

            GUILayout.EndHorizontal();

            var history = relation.History;
            if (history != null && history.Count > 0)
            {
                int start = history.Count > 4 ? history.Count - 4 : 0;
                for (int i = start; i < history.Count; i++)
                {
                    GUILayout.Label(history[i].ToObservationLine());
                }
            }

            GUILayout.EndVertical();
        }
    }
}
