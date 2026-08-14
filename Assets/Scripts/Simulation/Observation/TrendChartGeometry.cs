namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Maps history TotalDays / values into chart pixel space.
    /// X uses calendar TotalDays, never array index.
    /// </summary>
    public static class TrendChartGeometry
    {
        public static float MapX(int totalDays, int minDays, int maxDays, float x, float width)
        {
            if (width <= 0f)
            {
                return x;
            }

            if (maxDays <= minDays)
            {
                return x + width * 0.5f;
            }

            float t = (totalDays - minDays) / (float)(maxDays - minDays);
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return x + t * width;
        }

        public static float MapY(float value, float minValue, float maxValue, float y, float height)
        {
            if (height <= 0f)
            {
                return y;
            }

            if (maxValue <= minValue)
            {
                return y + height * 0.5f;
            }

            float t = (value - minValue) / (maxValue - minValue);
            if (t < 0f) t = 0f;
            if (t > 1f) t = 1f;
            return y + height - t * height;
        }

        public static void ValueRange(TrendPlotPoint[] points, out float min, out float max)
        {
            min = 0f;
            max = 1f;
            if (points == null || points.Length == 0)
            {
                return;
            }

            min = points[0].Value;
            max = points[0].Value;
            for (int i = 1; i < points.Length; i++)
            {
                float v = points[i].Value;
                if (v < min) min = v;
                if (v > max) max = v;
            }

            if (max <= min)
            {
                float pad = min == 0f ? 1f : (min < 0f ? -min * 0.08f : min * 0.08f);
                if (pad < 0.0001f)
                {
                    pad = 1f;
                }

                min -= pad;
                max += pad;
                return;
            }

            float span = max - min;
            min -= span * 0.08f;
            max += span * 0.08f;
            if (min > 0f && min / span < 0.15f)
            {
                min = 0f;
            }
        }
    }
}
