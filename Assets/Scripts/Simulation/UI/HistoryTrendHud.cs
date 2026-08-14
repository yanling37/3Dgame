using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;
using UnityEngine;

namespace DivineWorld.Simulation.UI
{
    public enum ObservationViewMode
    {
        SingleRegion = 0,
        CompareRegions = 1
    }

    /// <summary>
    /// P2-B v0.5 History / Report / Compare panel. Reads ObservationHistory only.
    /// Rebuilds when region, metric, range, mode, period, or history revision changes.
    /// </summary>
    public class HistoryTrendHud : MonoBehaviour
    {
        [SerializeField] ObservationHost observation;
        [SerializeField] bool visible = true;

        ObservationViewMode _mode = ObservationViewMode.SingleRegion;
        RegionId _region = RegionId.Theocracy;
        HistoryMetric _metric = HistoryMetric.Population;
        HistoryTimeRange _range = HistoryTimeRange.Recent1Year;
        ReportPeriod _period = ReportPeriod.Year;
        Vector2 _scroll;

        TrendSeries _series = TrendSeries.Empty;
        RegionCompare _compare;
        RegionReport _report;
        int _cachedRevision = int.MinValue;
        int _cachedTotalDays = int.MinValue;
        ObservationViewMode _cachedMode;
        RegionId _cachedRegion;
        HistoryMetric _cachedMetric;
        HistoryTimeRange _cachedRange;
        ReportPeriod _cachedPeriod;

        static readonly HistoryMetric[] ChartMetrics =
        {
            HistoryMetric.Population,
            HistoryMetric.Food,
            HistoryMetric.Water,
            HistoryMetric.Wood,
            HistoryMetric.Mineral,
            HistoryMetric.Magic,
            HistoryMetric.Disease,
            HistoryMetric.Stability,
            HistoryMetric.Education,
            HistoryMetric.Faith
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

        void ResetViewState()
        {
            _mode = ObservationViewMode.SingleRegion;
            _region = RegionId.Theocracy;
            _metric = HistoryMetric.Population;
            _range = HistoryTimeRange.Recent1Year;
            _period = ReportPeriod.Year;
            _scroll = Vector2.zero;
            _series = TrendSeries.Empty;
            _compare = null;
            _report = null;
            Invalidate();
        }

        void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            EnsureCaches();

            ObservationHudLayout.Compute(Screen.width, out _, out _, out float rightX, out float rightW);
            var area = new Rect(rightX, ObservationHudLayout.Pad, rightW, Screen.height - ObservationHudLayout.Pad * 2f);
            GUI.Box(area, GUIContent.none);

            GUILayout.BeginArea(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f));
            _scroll = GUILayout.BeginScrollView(_scroll);

            GUILayout.Label(ObservationVersion.HudTitle);
            GUILayout.Label("History / Report / Compare");

            GUILayout.Space(4);
            GUILayout.BeginHorizontal();
            ModeBtn("Single Region", ObservationViewMode.SingleRegion);
            ModeBtn("Compare Regions", ObservationViewMode.CompareRegions);
            GUILayout.EndHorizontal();

            if (_mode == ObservationViewMode.SingleRegion)
            {
                DrawSingle();
            }
            else
            {
                DrawCompare();
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void EnsureCaches()
        {
            var history = observation != null ? observation.Session.History : null;
            int revision = history != null ? history.Revision : -1;
            int totalDays = history != null ? history.LastTotalDays : -1;

            if (_cachedTotalDays > 0 && totalDays < _cachedTotalDays)
            {
                ResetViewState();
                revision = history != null ? history.Revision : -1;
                totalDays = history != null ? history.LastTotalDays : -1;
            }

            if (_series != null
                && revision == _cachedRevision
                && _mode == _cachedMode
                && _region == _cachedRegion
                && _metric == _cachedMetric
                && _range == _cachedRange
                && _period == _cachedPeriod)
            {
                return;
            }

            _cachedRevision = revision;
            _cachedTotalDays = totalDays;
            _cachedMode = _mode;
            _cachedRegion = _region;
            _cachedMetric = _metric;
            _cachedRange = _range;
            _cachedPeriod = _period;

            if (history == null)
            {
                _series = TrendSeries.Empty;
                _compare = null;
                _report = null;
                return;
            }

            _series = history.Query(_region, _metric, _range, 360);
            _compare = history.QueryCompare(_metric, _range, 360);
            var snap = observation != null ? observation.Current : null;
            _report = RegionReportBuilder.Build(history, snap, _region, _period);
        }

        void DrawSingle()
        {
            GUILayout.Space(4);
            GUILayout.Label("Region");
            GUILayout.BeginHorizontal();
            RegionBtn("Theocracy", RegionId.Theocracy);
            RegionBtn("Empire", RegionId.Empire);
            RegionBtn("Sea", RegionId.Sea);
            GUILayout.EndHorizontal();

            DrawMetricButtons();
            DrawRangeButtons();
            DrawCurrentTime();
            DrawChart(new[] { _series }, true);
            DrawEventMarkers();
            DrawReport();
        }

        void DrawCompare()
        {
            GUILayout.Space(4);
            GUILayout.Label("Metric (all regions, same range)");
            DrawMetricButtons();
            DrawRangeButtons();

            GUILayout.Space(6);
            if (_compare == null || !_compare.HasData)
            {
                GUILayout.Label("Viewing: (no history yet)");
                return;
            }

            GUILayout.Label("Shared range: " + _compare.ActualRangeLabel);
            GUILayout.Label(HistoryMetrics.AxisTitle(_compare.Metric));
            GUILayout.Space(4);

            DrawCompareTable();
            DrawChart(_compare.Series, false);
            GUILayout.Label("Legend: gold Theocracy · blue Empire · teal Sea. Curves are not merged.");
        }

        void DrawCompareTable()
        {
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(_compare.Metric + "  current");
            DrawCompareRow(RegionId.Theocracy);
            DrawCompareRow(RegionId.Empire);
            DrawCompareRow(RegionId.Sea);
            GUILayout.EndVertical();
        }

        void DrawCompareRow(RegionId id)
        {
            var series = _compare.For(id);
            string value = series.HasData
                ? HistoryMetrics.FormatValue(_compare.Metric, _compare.Current(id))
                : "—";
            GUILayout.Label(id.ToString().PadRight(12) + "  " + value + "  " + HistoryMetrics.UnitLabel(_compare.Metric));
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

        void DrawChart(TrendSeries[] series, bool drawEvents)
        {
            GUILayout.Space(4);
            float chartH = Mathf.Clamp(Screen.height * 0.38f, 240f, 360f);
            var reserved = GUILayoutUtility.GetRect(10f, chartH, GUILayout.ExpandWidth(true), GUILayout.MinHeight(220f));
            if (Event.current.type == EventType.Repaint)
            {
                ObservationChartGui.Paint(reserved, _metric, series, drawEvents);
            }
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

        void DrawReport()
        {
            GUILayout.Space(10);
            GUILayout.Label("Region Report");
            GUILayout.BeginHorizontal();
            PeriodBtn("Year", ReportPeriod.Year);
            PeriodBtn("Season", ReportPeriod.Season);
            GUILayout.EndHorizontal();

            if (_report == null || _report.Lines.Length == 0)
            {
                GUILayout.Label("（等待历史数据）");
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label(_report.Title);
            if (!_report.HasPrevious)
            {
                GUILayout.Label("Previous period: not enough history (showing current values only)");
            }
            else
            {
                GUILayout.Label("Compared with TotalDays " + _report.PreviousTotalDays);
            }

            for (int i = 0; i < _report.Lines.Length; i++)
            {
                var line = _report.Lines[i];
                string change = line.HasPrevious
                    ? line.TrendMark + " " + line.Percent.ToString("+0.0;-0.0") + "%"
                    : "n/a";
                string warn = line.Warning ? "  Warning" : "";
                GUILayout.Label(
                    HistoryMetrics.DisplayName(line.Metric)
                    + "   "
                    + HistoryMetrics.FormatValue(line.Metric, line.Current)
                    + "   "
                    + change
                    + warn);
            }

            GUILayout.Space(4);
            GUILayout.Label("Major events");
            if (_report.MajorEvents.Length == 0)
            {
                GUILayout.Label("（本阶段无重大事件）");
            }
            else
            {
                for (int i = 0; i < _report.MajorEvents.Length; i++)
                {
                    var e = _report.MajorEvents[i];
                    GUILayout.Label("• " + ObservationEventRecord.DisplayName(e.EventType)
                        + "  " + e.RegionId
                        + "  start " + e.StartDay
                        + "  dur " + e.Duration);
                }
            }

            GUILayout.Space(4);
            GUILayout.Label("Turning points");
            for (int i = 0; i < _report.TurningPoints.Length; i++)
            {
                var t = _report.TurningPoints[i];
                GUILayout.Label("• " + t.Label + "  " + t.TimeLabel + "  "
                    + HistoryMetrics.FormatValue(t.Metric, t.Value));
            }

            GUILayout.EndVertical();
        }

        void DrawMetricButtons()
        {
            GUILayout.Space(4);
            GUILayout.Label("Metric");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < 5 && i < ChartMetrics.Length; i++)
            {
                MetricBtn(ChartMetrics[i]);
            }

            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            for (int i = 5; i < ChartMetrics.Length; i++)
            {
                MetricBtn(ChartMetrics[i]);
            }

            GUILayout.EndHorizontal();
        }

        void DrawRangeButtons()
        {
            GUILayout.Space(4);
            GUILayout.Label("Time Range");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < Ranges.Length; i++)
            {
                RangeBtn(Ranges[i]);
            }

            GUILayout.EndHorizontal();
        }

        void ModeBtn(string label, ObservationViewMode mode)
        {
            Toggle(label, _mode == mode, () => _mode = mode);
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

        void PeriodBtn(string label, ReportPeriod period)
        {
            Toggle(label, _period == period, () => _period = period);
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
    }
}
