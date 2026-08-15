using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.UI
{
    /// <summary>
    /// Keeps the v0.3 region panel and v0.4 history panel from covering each other.
    /// Also draws the shared Year / Season / Day clock so date text cannot stack.
    /// </summary>
    public static class ObservationHudLayout
    {
        public const float Pad = 12f;
        public const float Gap = 12f;

        static GUIStyle _yearStyle;
        static GUIStyle _seasonStyle;
        static GUIStyle _dayStyle;
        static GUIStyle _captionStyle;
        static GUIStyle _axisStyle;
        static GUIStyle _axisRightStyle;
        static GUIStyle _chartTitleStyle;
        static GUIStyle _tooltipStyle;
        static bool _stylesReady;

        public static void Compute(float screenWidth, out float leftX, out float leftW, out float rightX, out float rightW)
        {
            float available = Mathf.Max(320f, screenWidth - Pad * 2f);
            if (available < 760f)
            {
                leftW = (available - Gap) * 0.42f;
                rightW = available - Gap - leftW;
                leftX = Pad;
                rightX = leftX + leftW + Gap;
                return;
            }

            leftW = Mathf.Min(460f, available * 0.38f);
            rightW = Mathf.Min(560f, available - leftW - Gap);
            leftX = Pad;
            rightX = screenWidth - Pad - rightW;
            if (rightX < leftX + leftW + Gap)
            {
                rightX = leftX + leftW + Gap;
                rightW = Mathf.Max(300f, screenWidth - Pad - rightX);
            }
        }

        /// <summary>
        /// Bottom-center P2-C politics panel. Does not change v0.3/v0.5 panel widths.
        /// </summary>
        public static Rect PoliticsPanel(float screenWidth, float screenHeight)
        {
            float width = Mathf.Clamp(screenWidth * 0.42f, 420f, 560f);
            if (width > screenWidth - Pad * 2f)
            {
                width = Mathf.Max(320f, screenWidth - Pad * 2f);
            }

            float height = Mathf.Clamp(screenHeight * 0.34f, 240f, 320f);
            float x = (screenWidth - width) * 0.5f;
            float y = screenHeight - height - Pad;
            return new Rect(x, y, width, height);
        }

        public static void DrawCalendarClock(int year, SeasonId season, int dayOfYear, int dayInSeason = -1)
        {
            EnsureStyles();
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.MinWidth(86f));
            GUILayout.Label("Year " + year, _yearStyle);
            GUILayout.EndVertical();

            GUILayout.Space(10f);

            var prev = GUI.color;
            GUI.color = SeasonTint(season);
            GUILayout.BeginVertical(GUILayout.MinWidth(100f));
            GUILayout.Label(SeasonName(season), _seasonStyle);
            GUILayout.EndVertical();
            GUI.color = prev;

            GUILayout.Space(10f);

            GUILayout.BeginVertical(GUILayout.MinWidth(86f));
            GUILayout.Label("Day " + dayOfYear, _dayStyle);
            if (dayInSeason > 0)
            {
                GUILayout.Label(dayInSeason + " / " + SimulationConfig.DaysPerSeason + " in season", _captionStyle);
            }

            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        public static GUIStyle AxisLabelStyle
        {
            get
            {
                EnsureStyles();
                return _axisStyle;
            }
        }

        public static GUIStyle AxisRightStyle
        {
            get
            {
                EnsureStyles();
                return _axisRightStyle;
            }
        }

        public static GUIStyle ChartTitleStyle
        {
            get
            {
                EnsureStyles();
                return _chartTitleStyle;
            }
        }

        public static GUIStyle TooltipStyle
        {
            get
            {
                EnsureStyles();
                return _tooltipStyle;
            }
        }

        public static Color SeasonTint(SeasonId season)
        {
            switch (season)
            {
                case SeasonId.Spring: return new Color(0.62f, 0.88f, 0.62f);
                case SeasonId.Summer: return new Color(0.95f, 0.84f, 0.48f);
                case SeasonId.Autumn: return new Color(0.92f, 0.68f, 0.42f);
                case SeasonId.Winter: return new Color(0.72f, 0.84f, 0.96f);
                default: return Color.white;
            }
        }

        public static string SeasonName(SeasonId season)
        {
            switch (season)
            {
                case SeasonId.Spring: return "Spring";
                case SeasonId.Summer: return "Summer";
                case SeasonId.Autumn: return "Autumn";
                case SeasonId.Winter: return "Winter";
                default: return season.ToString();
            }
        }

        static void EnsureStyles()
        {
            if (_stylesReady && _yearStyle != null)
            {
                return;
            }

            _yearStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip
            };

            _seasonStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                clipping = TextClipping.Clip
            };

            _dayStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip
            };

            _captionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.UpperLeft,
                wordWrap = false,
                clipping = TextClipping.Clip
            };
            _captionStyle.normal.textColor = new Color(0.75f, 0.78f, 0.82f);

            _axisStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.UpperCenter,
                wordWrap = true,
                clipping = TextClipping.Clip
            };

            _axisRightStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleRight,
                wordWrap = false,
                clipping = TextClipping.Clip
            };

            _chartTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip
            };

            _tooltipStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 12,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                padding = new RectOffset(8, 8, 6, 6)
            };

            _stylesReady = true;
        }
    }
}
