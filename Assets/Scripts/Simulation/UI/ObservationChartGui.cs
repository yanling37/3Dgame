using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;
using UnityEngine;

namespace DivineWorld.Simulation.UI
{
    /// <summary>
    /// Shared IMGUI trend painter: Y axis, units, legend, tooltip.
    /// Does not allocate chart GameObjects.
    /// </summary>
    public static class ObservationChartGui
    {
        public static readonly Color Theocracy = new Color(0.90f, 0.76f, 0.32f);
        public static readonly Color Empire = new Color(0.52f, 0.62f, 0.95f);
        public static readonly Color Sea = new Color(0.28f, 0.78f, 0.84f);

        public static Color ColorFor(RegionId id)
        {
            switch (id)
            {
                case RegionId.Theocracy: return Theocracy;
                case RegionId.Empire: return Empire;
                case RegionId.Sea: return Sea;
                default: return Color.white;
            }
        }

        public static void Paint(
            Rect rect,
            HistoryMetric metric,
            TrendSeries[] series,
            bool drawEvents)
        {
            FillRect(rect, new Color(0.12f, 0.14f, 0.18f, 0.92f));
            if (series == null || !AnyData(series))
            {
                GUI.Label(
                    new Rect(rect.x + 12f, rect.y + 12f, rect.width - 24f, 24f),
                    "Waiting for history ticks…",
                    ObservationHudLayout.ChartTitleStyle);
                return;
            }

            float padL = 64f;
            float padR = 14f;
            float padT = 44f;
            float padB = 44f;
            float plotX = rect.x + padL;
            float plotY = rect.y + padT;
            float plotW = Mathf.Max(16f, rect.width - padL - padR);
            float plotH = Mathf.Max(16f, rect.height - padT - padB);
            var plot = new Rect(plotX, plotY, plotW, plotH);

            int minDay = int.MaxValue;
            int maxDay = int.MinValue;
            for (int i = 0; i < series.Length; i++)
            {
                if (series[i] == null || !series[i].HasData)
                {
                    continue;
                }

                if (series[i].FirstTotalDays < minDay) minDay = series[i].FirstTotalDays;
                if (series[i].LastTotalDays > maxDay) maxDay = series[i].LastTotalDays;
            }

            if (minDay == int.MaxValue)
            {
                return;
            }

            TrendChartGeometry.ValueRange(series, out float minV, out float maxV);

            GUI.Label(
                new Rect(rect.x + 8f, rect.y + 4f, rect.width - 16f, 20f),
                HistoryMetrics.AxisTitle(metric),
                ObservationHudLayout.ChartTitleStyle);

            DrawLegend(new Rect(rect.x + 8f, rect.y + 22f, rect.width - 16f, 20f), series, metric);

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
                    HistoryMetrics.FormatValue(metric, yTicks[i]),
                    ObservationHudLayout.AxisRightStyle);
            }

            FillRect(new Rect(plot.x, plot.yMax, plot.width, 1.5f), axis);
            FillRect(new Rect(plot.x, plot.y, 1.5f, plot.height), axis);

            for (int s = 0; s < series.Length; s++)
            {
                var pts = series[s] != null ? series[s].PlotPoints : null;
                if (pts == null || pts.Length == 0)
                {
                    continue;
                }

                var prev = GUI.color;
                GUI.color = ColorFor(series[s].RegionId);
                for (int i = 1; i < pts.Length; i++)
                {
                    float x0 = TrendChartGeometry.MapX(pts[i - 1].TotalDays, minDay, maxDay, plot.x, plot.width);
                    float y0 = TrendChartGeometry.MapY(pts[i - 1].Value, minV, maxV, plot.y, plot.height);
                    float x1 = TrendChartGeometry.MapX(pts[i].TotalDays, minDay, maxDay, plot.x, plot.width);
                    float y1 = TrendChartGeometry.MapY(pts[i].Value, minV, maxV, plot.y, plot.height);
                    DrawLine(new Vector2(x0, y0), new Vector2(x1, y1), 2.2f);
                }

                GUI.color = prev;
            }

            if (drawEvents)
            {
                for (int s = 0; s < series.Length; s++)
                {
                    var markers = series[s] != null ? series[s].EventMarkers : null;
                    if (markers == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < markers.Length; i++)
                    {
                        float mx = TrendChartGeometry.MapX(markers[i].MarkerTotalDays, minDay, maxDay, plot.x, plot.width);
                        FillRect(new Rect(mx, plot.y, 1.5f, plot.height), new Color(1f, 0.7f, 0.3f, 0.55f));
                    }
                }
            }

            DrawXAxisLabels(plot, minDay, maxDay, FirstAxis(series));
            DrawHoverTooltip(plot, minDay, maxDay, minV, maxV, metric, series);
        }

        static void DrawLegend(Rect rect, TrendSeries[] series, HistoryMetric metric)
        {
            float x = rect.x;
            for (int i = 0; i < series.Length; i++)
            {
                if (series[i] == null)
                {
                    continue;
                }

                var color = ColorFor(series[i].RegionId);
                FillRect(new Rect(x, rect.y + 5f, 12f, 8f), color);
                string value = series[i].HasData
                    ? HistoryMetrics.FormatValue(metric, series[i].Last.Read(metric))
                    : "—";
                string text = series[i].RegionId + "  " + value;
                GUI.Label(new Rect(x + 16f, rect.y, 150f, 18f), text);
                x += 168f;
            }
        }

        static void DrawXAxisLabels(Rect plot, int minDay, int maxDay, TrendAxisLabel[] labels)
        {
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

        static void DrawHoverTooltip(
            Rect plot,
            int minDay,
            int maxDay,
            float minV,
            float maxV,
            HistoryMetric metric,
            TrendSeries[] series)
        {
            Vector2 mouse = Event.current.mousePosition;
            if (!plot.Contains(mouse))
            {
                return;
            }

            int nearestDay = minDay;
            float best = float.MaxValue;
            TrendPlotPoint nearestPt = null;
            for (int s = 0; s < series.Length; s++)
            {
                var pts = series[s] != null ? series[s].PlotPoints : null;
                if (pts == null)
                {
                    continue;
                }

                for (int i = 0; i < pts.Length; i++)
                {
                    float px = TrendChartGeometry.MapX(pts[i].TotalDays, minDay, maxDay, plot.x, plot.width);
                    float dist = Mathf.Abs(px - mouse.x);
                    if (dist < best)
                    {
                        best = dist;
                        nearestDay = pts[i].TotalDays;
                        nearestPt = pts[i];
                    }
                }
            }

            if (nearestPt == null)
            {
                return;
            }

            float hx = TrendChartGeometry.MapX(nearestDay, minDay, maxDay, plot.x, plot.width);
            FillRect(new Rect(hx, plot.y, 1f, plot.height), new Color(1f, 1f, 1f, 0.35f));

            var season = WorldState.SeasonFromDayOfYear(nearestPt.DayOfYear);
            var sb = HistoryMetrics.FormatCalendar(nearestPt.Year, season, nearestPt.DayOfYear);
            int lines = 1;
            for (int s = 0; s < series.Length; s++)
            {
                float value;
                if (!TryValueAt(series[s], nearestDay, out value))
                {
                    continue;
                }

                float hy = TrendChartGeometry.MapY(value, minV, maxV, plot.y, plot.height);
                FillRect(new Rect(hx - 3.5f, hy - 3.5f, 7f, 7f), ColorFor(series[s].RegionId));
                sb += "\n"
                    + series[s].RegionId
                    + "  "
                    + HistoryMetrics.FormatValue(metric, value)
                    + " "
                    + HistoryMetrics.UnitLabel(metric);
                lines++;
            }

            const float tipW = 220f;
            float tipH = 18f + lines * 16f;
            float tx = Mathf.Clamp(mouse.x + 12f, plot.x, plot.xMax - tipW);
            float ty = Mathf.Clamp(mouse.y - tipH - 8f, plot.y, plot.yMax - tipH);
            GUI.Box(new Rect(tx, ty, tipW, tipH), sb, ObservationHudLayout.TooltipStyle);
        }

        static bool TryValueAt(TrendSeries series, int totalDays, out float value)
        {
            value = 0f;
            if (series == null || series.PlotPoints == null || series.PlotPoints.Length == 0)
            {
                return false;
            }

            int best = 0;
            int bestDelta = int.MaxValue;
            for (int i = 0; i < series.PlotPoints.Length; i++)
            {
                int d = series.PlotPoints[i].TotalDays - totalDays;
                if (d < 0) d = -d;
                if (d < bestDelta)
                {
                    bestDelta = d;
                    best = i;
                }
            }

            if (bestDelta > 5)
            {
                return false;
            }

            value = series.PlotPoints[best].Value;
            return true;
        }

        static TrendAxisLabel[] FirstAxis(TrendSeries[] series)
        {
            for (int i = 0; i < series.Length; i++)
            {
                if (series[i] != null && series[i].AxisLabels != null && series[i].AxisLabels.Length > 0)
                {
                    return series[i].AxisLabels;
                }
            }

            return null;
        }

        static bool AnyData(TrendSeries[] series)
        {
            for (int i = 0; i < series.Length; i++)
            {
                if (series[i] != null && series[i].HasData && series[i].PlotPoints.Length > 0)
                {
                    return true;
                }
            }

            return false;
        }

        public static void FillRect(Rect rect, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        public static void DrawLine(Vector2 a, Vector2 b, float thickness)
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
