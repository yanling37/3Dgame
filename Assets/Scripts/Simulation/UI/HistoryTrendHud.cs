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
            GUI.Box(area, GUIContent.none);

            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f));
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
            int dayInSeason = ((last.DayOfYear - 1) % SimulationConfig.DaysPerSeason) + 1;
            ObservationHudLayout.DrawCalendarClock(last.Year, last.Season, last.DayOfYear, dayInSeason);
            GUILayout.Label("Range: " + _series.ActualRangeLabel);
            GUILayout.Label("Region " + _series.RegionId + "  ·  " + HistoryMetrics.AxisTitle(_series.Metric));
        }

        void DrawChart()
        {
            GUILayout.Space(4);
            float chartH = Mathf.Clamp(Screen.height * 0.42f, 260f, 380f);
            var reserved = GUILayoutUtility.GetRect(10f, chartH, GUILayout.ExpandWidth(true), GUILayout.MinHeight(240f));
            if (Event.current.type == EventType.Repaint)
            {
                PaintChart(reserved);
            }
        }

        void PaintChart(Rect rect)
        {
            FillRect(rect, new Color(0.12f, 0.14f, 0.18f, 0.92f));
            if (_series == null || !_series.HasData || _series.PlotPoints.Length == 0)
            {
                GUI.Label(
                    new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, 24f),
                    "Waiting for history ticks…",
                    ObservationHudLayout.ChartTitleStyle);
                return;
            }

            float padL = 64f;
            float padR = 14f;
            float padT = 28f;
            float padB = 44f;
            float plotX = rect.x + padL;
            float plotY = rect.y + padT;
            float plotW = Mathf.Max(16f, rect.width - padL - padR);
            float plotH = Mathf.Max(16f, rect.height - padT - padB);
            var plot = new Rect(plotX, plotY, plotW, plotH);

            int minDay = _series.FirstTotalDays;
            int maxDay = _series.LastTotalDays;
            TrendChartGeometry.ValueRange(_series.PlotPoints, out float minV, out float maxV);

            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, 20f),
                HistoryMetrics.AxisTitle(_series.Metric),
                ObservationHudLayout.ChartTitleStyle);

            var yTicks = new float[8];
            int yCount = TrendChartGeometry.BuildNiceTicks(minV, maxV, 5, yTicks);
            var grid = new Color(1f, 1f, 1f, 0.12f);
            var axis = new Color(0.82f, 0.86f, 0.9f, 0.9f);

            for (int i = 0; i < yCount; i++)
            {
                float gy = TrendChartGeometry.MapY(yTicks[i], minV, maxV, plot.y, plot.height);
                FillRect(new Rect(plot.x, gy, plot.width, 1f), grid);
                GUI.Label(
                    new Rect(rect.x + 4f, gy - 8f, padL - 10f, 16f),
                    HistoryMetrics.FormatValue(_series.Metric, yTicks[i]),
                    ObservationHudLayout.AxisRightStyle);
            }

            FillRect(new Rect(plot.x, plot.yMax, plot.width, 1.5f), axis);
            FillRect(new Rect(plot.x, plot.y, 1.5f, plot.height), axis);

            var points = _series.PlotPoints;
            var prevColor = GUI.color;
            GUI.color = new Color(0.45f, 0.75f, 0.95f, 1f);
            for (int i = 1; i < points.Length; i++)
            {
                float x0 = TrendChartGeometry.MapX(points[i - 1].TotalDays, minDay, maxDay, plot.x, plot.width);
                float y0 = TrendChartGeometry.MapY(points[i - 1].Value, minV, maxV, plot.y, plot.height);
                float x1 = TrendChartGeometry.MapX(points[i].TotalDays, minDay, maxDay, plot.x, plot.width);
                float y1 = TrendChartGeometry.MapY(points[i].Value, minV, maxV, plot.y, plot.height);
                DrawLine(new Vector2(x0, y0), new Vector2(x1, y1), 2f);
            }

            GUI.color = prevColor;

            var markers = _series.EventMarkers;
            for (int i = 0; i < markers.Length; i++)
            {
                float mx = TrendChartGeometry.MapX(markers[i].MarkerTotalDays, minDay, maxDay, plot.x, plot.width);
                GUI.color = MarkerColor(markers[i].EventType);
                FillRect(new Rect(mx, plot.y, 1.5f, plot.height), GUI.color);
                GUI.DrawTexture(new Rect(mx - 4f, plot.y - 6f, 8f, 8f), Texture2D.whiteTexture);
            }

            GUI.color = prevColor;

            DrawXAxisLabels(plot, minDay, maxDay);

            DrawHoverTooltip(plot, minDay, maxDay, minV, maxV);
        }

        void DrawXAxisLabels(Rect plot, int minDay, int maxDay)
        {
            var labels = _series.AxisLabels;
            if (labels == null || labels.Length == 0)
            {
                return;
            }

            float minGap = 76f;
            DrawOneXLabel(plot, minDay, maxDay, labels[0]);
            if (labels.Length == 1)
            {
                return;
            }

            float firstX = TrendChartGeometry.MapX(labels[0].TotalDays, minDay, maxDay, plot.x, plot.width);
            float lastX = TrendChartGeometry.MapX(labels[labels.Length - 1].TotalDays, minDay, maxDay, plot.x, plot.width);
            float prevX = firstX;
            for (int i = 1; i < labels.Length - 1; i++)
            {
                float lx = TrendChartGeometry.MapX(labels[i].TotalDays, minDay, maxDay, plot.x, plot.width);
                if (lx - prevX < minGap || lastX - lx < minGap)
                {
                    continue;
                }

                DrawOneXLabel(plot, minDay, maxDay, labels[i]);
                prevX = lx;
            }

            if (lastX - firstX >= minGap * 0.5f)
            {
                DrawOneXLabel(plot, minDay, maxDay, labels[labels.Length - 1]);
            }
        }

        static void DrawOneXLabel(Rect plot, int minDay, int maxDay, TrendAxisLabel label)
        {
            float lx = TrendChartGeometry.MapX(label.TotalDays, minDay, maxDay, plot.x, plot.width);
            var labelRect = new Rect(lx - 38f, plot.yMax + 4f, 76f, 36f);
            GUI.Label(
                labelRect,
                HistoryMetrics.CompactAxisLabel(label.Year, label.DayOfYear),
                ObservationHudLayout.AxisLabelStyle);
        }

        void DrawHoverTooltip(Rect plot, int minDay, int maxDay, float minV, float maxV)
        {
            Vector2 mouse = Event.current.mousePosition;
            if (!plot.Contains(mouse) || _series.PlotPoints.Length == 0)
            {
                return;
            }

            var points = _series.PlotPoints;
            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < points.Length; i++)
            {
                float px = TrendChartGeometry.MapX(points[i].TotalDays, minDay, maxDay, plot.x, plot.width);
                float dist = Mathf.Abs(px - mouse.x);
                if (dist < best)
                {
                    best = dist;
                    nearest = i;
                }
            }

            var p = points[nearest];
            float hx = TrendChartGeometry.MapX(p.TotalDays, minDay, maxDay, plot.x, plot.width);
            float hy = TrendChartGeometry.MapY(p.Value, minV, maxV, plot.y, plot.height);
            FillRect(new Rect(hx - 3.5f, hy - 3.5f, 7f, 7f), new Color(1f, 1f, 1f, 0.95f));

            var season = WorldState.SeasonFromDayOfYear(p.DayOfYear);
            string text = HistoryMetrics.FormatCalendar(p.Year, season, p.DayOfYear)
                + "\n"
                + HistoryMetrics.DisplayName(_series.Metric)
                + ": "
                + HistoryMetrics.FormatValue(_series.Metric, p.Value)
                + " "
                + HistoryMetrics.UnitLabel(_series.Metric);

            const float tipW = 196f;
            const float tipH = 44f;
            float tx = Mathf.Clamp(mouse.x + 12f, plot.x, plot.xMax - tipW);
            float ty = Mathf.Clamp(mouse.y - tipH - 8f, plot.y, plot.yMax - tipH);
            GUI.Box(new Rect(tx, ty, tipW, tipH), text, ObservationHudLayout.TooltipStyle);
        }

        static void FillRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
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
                    + "  duration "
                    + m.Record.Duration
                    + "  @ TotalDays "
                    + m.Record.StartDay);
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
