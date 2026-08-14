using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;
using UnityEngine;

namespace DivineWorld.Simulation.UI
{
    /// <summary>
    /// P2-B v0.4 History / Trend panel. Reads ObservationHistory only.
    /// Chart rebuilds when region, metric, range, or history revision changes — not every frame.
    /// </summary>
    public class HistoryTrendHud : MonoBehaviour
    {
        [SerializeField] ObservationHost observation;
        [SerializeField] bool visible = true;

        RegionId _region = RegionId.Theocracy;
        HistoryMetric _metric = HistoryMetric.Population;
        HistoryTimeRange _range = HistoryTimeRange.Recent1Year;
        Vector2 _scroll;
        TrendSeries _series = TrendSeries.Empty;
        int _cachedRevision = int.MinValue;
        RegionId _cachedRegion;
        HistoryMetric _cachedMetric;
        HistoryTimeRange _cachedRange;

        static readonly HistoryMetric[] Metrics =
        {
            HistoryMetric.Population,
            HistoryMetric.Food,
            HistoryMetric.Water,
            HistoryMetric.Disease,
            HistoryMetric.Stability,
            HistoryMetric.Magic
        };

        static readonly HistoryTimeRange[] Ranges =
        {
            HistoryTimeRange.Recent30Days,
            HistoryTimeRange.Recent90Days,
            HistoryTimeRange.Recent1Year,
            HistoryTimeRange.AllHistory
        };

        public void Bind(ObservationHost observationHost)
        {
            observation = observationHost;
            Invalidate();
        }

        void Start()
        {
            if (observation == null)
            {
                observation = FindObjectOfType<ObservationHost>();
            }
        }

        void Invalidate()
        {
            _cachedRevision = int.MinValue;
        }

        void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            EnsureSeries();

            ObservationHudLayout.Compute(Screen.width, out _, out _, out float rightX, out float rightW);
            var area = new Rect(rightX, ObservationHudLayout.Pad, rightW, Screen.height - ObservationHudLayout.Pad * 2f);
            GUI.Box(area, ObservationVersion.HudTitle);

            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 26f, area.width - 20f, area.height - 34f));
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label(ObservationVersion.HudTitle);
            GUILayout.Label("History / Trend");

            GUILayout.Space(4);
            GUILayout.Label("Region");
            GUILayout.BeginHorizontal();
            RegionBtn("Theocracy", RegionId.Theocracy);
            RegionBtn("Empire", RegionId.Empire);
            RegionBtn("Sea", RegionId.Sea);
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("Metric");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Metrics.Length; i++)
            {
                MetricBtn(Metrics[i]);
            }

            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("Time Range");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Ranges.Length; i++)
            {
                RangeBtn(Ranges[i]);
            }

            GUILayout.EndHorizontal();

            DrawCurrentTime();
            GUILayout.Space(6);
            DrawChart();
            GUILayout.Space(6);
            DrawEventMarkers();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void EnsureSeries()
        {
            var history = observation != null ? observation.Session.History : null;
            int revision = history != null ? history.Revision : -1;
            if (_series != null
                && revision == _cachedRevision
                && _region == _cachedRegion
                && _metric == _cachedMetric
                && _range == _cachedRange)
            {
                return;
            }

            _cachedRevision = revision;
            _cachedRegion = _region;
            _cachedMetric = _metric;
            _cachedRange = _range;
            _series = history != null
                ? history.Query(_region, _metric, _range, 360)
                : TrendSeries.Empty;
        }

        void DrawCurrentTime()
        {
            GUILayout.Space(6);
            if (_series == null || !_series.HasData)
            {
                GUILayout.Label("Viewing: (no history yet)");
                GUILayout.Label("Available range: none");
                return;
            }

            var last = _series.Last;
            GUILayout.Label("Viewing: " + last.TimeLabel + "  ·  " + last.Season + "  ·  TotalDays " + last.TotalDays);
            GUILayout.Label("Available: " + _series.ActualRangeLabel);
            GUILayout.Label("Region " + _series.RegionId + "  ·  " + HistoryMetrics.DisplayName(_series.Metric));
        }

        void DrawChart()
        {
            GUILayout.Label("Trend Chart");
            var reserved = GUILayoutUtility.GetRect(10f, 180f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                PaintChart(reserved);
            }
        }

        void PaintChart(Rect rect)
        {
            GUI.Box(rect, GUIContent.none);
            if (_series == null || !_series.HasData || _series.PlotPoints.Length == 0)
            {
                GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 24f), "Waiting for history ticks…");
                return;
            }

            float padL = 8f;
            float padR = 8f;
            float padT = 18f;
            float padB = 28f;
            float x = rect.x + padL;
            float y = rect.y + padT;
            float w = Mathf.Max(8f, rect.width - padL - padR);
            float h = Mathf.Max(8f, rect.height - padT - padB);

            int minDay = _series.FirstTotalDays;
            int maxDay = _series.LastTotalDays;
            TrendChartGeometry.ValueRange(_series.PlotPoints, out float minV, out float maxV);

            var prevColor = GUI.color;
            GUI.color = new Color(0.35f, 0.55f, 0.75f, 1f);
            var points = _series.PlotPoints;
            for (int i = 1; i < points.Length; i++)
            {
                float x0 = TrendChartGeometry.MapX(points[i - 1].TotalDays, minDay, maxDay, x, w);
                float y0 = TrendChartGeometry.MapY(points[i - 1].Value, minV, maxV, y, h);
                float x1 = TrendChartGeometry.MapX(points[i].TotalDays, minDay, maxDay, x, w);
                float y1 = TrendChartGeometry.MapY(points[i].Value, minV, maxV, y, h);
                DrawLine(new Vector2(x0, y0), new Vector2(x1, y1), 2f);
            }

            GUI.color = prevColor;

            var markers = _series.EventMarkers;
            for (int i = 0; i < markers.Length; i++)
            {
                float mx = TrendChartGeometry.MapX(markers[i].MarkerTotalDays, minDay, maxDay, x, w);
                GUI.color = MarkerColor(markers[i].EventType);
                DrawLine(new Vector2(mx, y), new Vector2(mx, y + h), 1.5f);
                GUI.DrawTexture(new Rect(mx - 4f, y - 6f, 8f, 8f), Texture2D.whiteTexture);
            }

            GUI.color = prevColor;

            var labels = _series.AxisLabels;
            for (int i = 0; i < labels.Length; i++)
            {
                float lx = TrendChartGeometry.MapX(labels[i].TotalDays, minDay, maxDay, x, w);
                var labelRect = new Rect(lx - 42f, rect.yMax - 24f, 84f, 20f);
                GUI.Label(labelRect, labels[i].Text);
            }

            GUI.Label(new Rect(rect.x + 8f, rect.y + 2f, rect.width - 16f, 16f),
                HistoryMetrics.DisplayName(_series.Metric)
                + "  "
                + points[points.Length - 1].Value.ToString("0.##"));
        }

        void DrawEventMarkers()
        {
            GUILayout.Label("Event Markers");
            if (_series == null || _series.EventMarkers.Length == 0)
            {
                GUILayout.Label("（该地区此时间范围内无 Natural Disaster / Disease Outbreak / Food Shortage）");
                return;
            }

            for (int i = 0; i < _series.EventMarkers.Length; i++)
            {
                var m = _series.EventMarkers[i];
                GUILayout.Label(
                    "• "
                    + m.Label
                    + "  "
                    + m.Record.RegionId
                    + "  start TotalDays "
                    + m.Record.StartDay
                    + "  duration "
                    + m.Record.Duration
                    + "  @ "
                    + m.TimeLabel);
            }
        }

        void RegionBtn(string label, RegionId id)
        {
            Toggle(label, _region == id, () => _region = id);
        }

        void MetricBtn(HistoryMetric metric)
        {
            Toggle(HistoryMetrics.DisplayName(metric), _metric == metric, () => _metric = metric);
        }

        void RangeBtn(HistoryTimeRange range)
        {
            Toggle(HistoryMetrics.DisplayName(range), _range == range, () => _range = range);
        }

        void Toggle(string label, bool on, System.Action select)
        {
            var prev = GUI.backgroundColor;
            if (on)
            {
                GUI.backgroundColor = new Color(0.55f, 0.8f, 1f);
            }

            if (GUILayout.Button(label, GUILayout.Height(24)))
            {
                select();
                Invalidate();
            }

            GUI.backgroundColor = prev;
        }

        static Color MarkerColor(SimEventType type)
        {
            switch (type)
            {
                case SimEventType.NaturalDisaster: return new Color(1f, 0.55f, 0.2f);
                case SimEventType.DiseaseOutbreak: return new Color(0.85f, 0.35f, 0.75f);
                case SimEventType.FoodShortage: return new Color(0.95f, 0.85f, 0.25f);
                default: return Color.white;
            }
        }

        static void DrawLine(Vector2 a, Vector2 b, float thickness)
        {
            var d = b - a;
            float len = Mathf.Sqrt(d.x * d.x + d.y * d.y);
            if (len < 0.5f)
            {
                return;
            }

            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            GUI.DrawTexture(new Rect(a.x, a.y - thickness * 0.5f, len, thickness), Texture2D.whiteTexture);
            GUI.matrix = matrix;
        }
    }
}
