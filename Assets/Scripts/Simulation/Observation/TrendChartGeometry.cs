using System;

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

        /// <summary>
        /// Builds round Y-axis tick values between min and max. Returns count written into <paramref name="ticks"/>.
        /// </summary>
        public static int BuildNiceTicks(float min, float max, int maxCount, float[] ticks)
        {
            if (ticks == null || ticks.Length == 0)
            {
                return 0;
            }

            if (maxCount < 2)
            {
                maxCount = 2;
            }

            if (maxCount > ticks.Length)
            {
                maxCount = ticks.Length;
            }

            if (!(max > min))
            {
                ticks[0] = min;
                return 1;
            }

            float span = max - min;
            float rough = span / (maxCount - 1);
            if (rough <= 0f)
            {
                ticks[0] = min;
                ticks[1] = max;
                return 2;
            }

            double mag = Math.Pow(10.0, Math.Floor(Math.Log10(rough)));
            double residual = rough / mag;
            double nice;
            if (residual <= 1.0) nice = 1.0;
            else if (residual <= 2.0) nice = 2.0;
            else if (residual <= 5.0) nice = 5.0;
            else nice = 10.0;

            float step = (float)(nice * mag);
            if (step <= 0f)
            {
                ticks[0] = min;
                ticks[1] = max;
                return Math.Min(2, ticks.Length);
            }

            float start = (float)(Math.Ceiling(min / step) * step);
            if (start < min)
            {
                start += step;
            }

            int n = 0;
            for (float v = start; v <= max + step * 0.001f && n < maxCount; v += step)
            {
                ticks[n++] = v;
            }

            if (n == 0)
            {
                ticks[0] = min;
                if (ticks.Length > 1)
                {
                    ticks[1] = max;
                    return 2;
                }

                return 1;
            }

            return n;
        }
    }
}
