using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Observation;
using UnityEngine;

namespace DivineWorld.Simulation.UI
{
    /// <summary>
    /// P2-B Observation v0.2 HUD. Display values come only from ObservationHost snapshots.
    /// SimulationWorld is used solely for player controls (advance / reset / influence).
    /// Region details scroll inside a dedicated ScrollRect, not the whole screen.
    /// </summary>
    public class WorldObserverHud : MonoBehaviour
    {
        [SerializeField] SimulationWorld world;
        [SerializeField] bool visible = true;

        ObservationHost _observation;
        RegionObservationScrollView _regionScroll;

        public void Bind(SimulationWorld simulationWorld, ObservationHost observation)
        {
            world = simulationWorld;
            _observation = observation;
            EnsureRegionScroll();
        }

        void OnGUI()
        {
            if (!visible || world == null)
            {
                if (_regionScroll != null)
                {
                    _regionScroll.gameObject.SetActive(false);
                }

                return;
            }

            if (_regionScroll != null)
            {
                _regionScroll.gameObject.SetActive(true);
            }

            const float pad = 12f;
            float hudW = Mathf.Min(520f, Screen.width - pad * 2f);
            const float topH = 278f;
            const float bottomH = 228f;
            float midH = Mathf.Max(180f, Screen.height - pad * 2f - topH - bottomH);
            float midY = pad + topH;

            GUI.Box(new Rect(pad, pad, hudW, topH), ObservationLabels.UiVersion);
            GUILayout.BeginArea(new Rect(pad + 10f, pad + 28f, hudW - 20f, topH - 36f));
            DrawWorldHeader(_observation != null ? _observation.Current : null);
            DrawHistoryStatus();
            DrawControls();
            DrawRegionTabs();
            GUILayout.Label("—— 地区信息 ——");
            GUILayout.EndArea();

            EnsureRegionScroll();
            if (_regionScroll != null)
            {
                _regionScroll.SetImguiRect(new Rect(pad, midY, hudW, midH));
            }

            GUI.Box(new Rect(pad, midY + midH, hudW, bottomH), "");
            GUILayout.BeginArea(new Rect(pad + 10f, midY + midH + 8f, hudW - 20f, bottomH - 16f));
            DrawInfluence();
            GUILayout.EndArea();
        }

        void EnsureRegionScroll()
        {
            if (_regionScroll != null || _observation == null)
            {
                return;
            }

            _regionScroll = RegionObservationScrollView.Create(_observation);
        }

        void OnDestroy()
        {
            if (_regionScroll != null)
            {
                Destroy(_regionScroll.gameObject);
                _regionScroll = null;
            }
        }

        void DrawWorldHeader(WorldObservationSnapshot snap)
        {
            GUILayout.Label(ObservationPanelText.FormatWorldHeader(snap));
            if (snap != null && snap.HaltedOnNumericError)
            {
                GUILayout.Label("!! NUMERIC HALT !!");
                GUILayout.Label(snap.LastNumericError ?? "");
            }
        }

        void DrawHistoryStatus()
        {
            if (_observation == null)
            {
                return;
            }

            var hist = _observation.History;
            var latest = hist.Latest;
            int latestDays = latest != null ? latest.TotalDays : -1;
            string has30 = hist.TryGetExact(30) != null ? "yes" : "no";
            GUILayout.Label($"History {hist.Count} samples | latest TotalDays={latestDays} | Day30={has30}");
        }

        void DrawControls()
        {
            GUILayout.Space(6);
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

            if (GUILayout.Button("重置世界", GUILayout.Width(80)))
            {
                world.ResetWorld();
            }

            GUILayout.EndHorizontal();

            GUILayout.Label("速度（秒/日）");
            world.SecondsPerDay = GUILayout.HorizontalSlider(world.SecondsPerDay, 0.05f, 1.5f);
        }

        void DrawRegionTabs()
        {
            if (_observation?.Current?.Regions == null)
            {
                return;
            }

            GUILayout.Space(8);
            GUILayout.Label("选中地区");
            GUILayout.BeginHorizontal();
            var regions = _observation.Current.Regions;
            for (int i = 0; i < regions.Length; i++)
            {
                var region = regions[i];
                if (region == null)
                {
                    continue;
                }

                bool on = _observation.SelectedRegionId == region.RegionId;
                var prev = GUI.backgroundColor;
                if (on)
                {
                    GUI.backgroundColor = new Color(0.6f, 0.85f, 1f);
                }

                if (GUILayout.Button(region.DisplayName, GUILayout.Height(28)))
                {
                    _observation.SelectRegion(region.RegionId);
                }

                GUI.backgroundColor = prev;
            }

            GUILayout.EndHorizontal();
        }

        void DrawInfluence()
        {
            if (world?.Influence == null)
            {
                return;
            }

            GUILayout.Label("注视微调（写入模拟 Influence，非观察重算）");
            var inf = world.Influence;
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
        }
    }
}
