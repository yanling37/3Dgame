using UnityEngine;

namespace DivineWorld.Simulation.UI
{
    /// <summary>
    /// Keeps the v0.3 region panel and v0.4 history panel from covering each other.
    /// </summary>
    public static class ObservationHudLayout
    {
        public const float Pad = 12f;
        public const float Gap = 12f;

        public static void Compute(float screenWidth, out float leftX, out float leftW, out float rightX, out float rightW)
        {
            float available = Mathf.Max(320f, screenWidth - Pad * 2f);
            if (available < 760f)
            {
                leftW = (available - Gap) * 0.48f;
                rightW = available - Gap - leftW;
                leftX = Pad;
                rightX = leftX + leftW + Gap;
                return;
            }

            leftW = Mathf.Min(500f, available * 0.42f);
            rightW = Mathf.Min(480f, available - leftW - Gap);
            leftX = Pad;
            rightX = screenWidth - Pad - rightW;
            if (rightX < leftX + leftW + Gap)
            {
                rightX = leftX + leftW + Gap;
                rightW = Mathf.Max(280f, screenWidth - Pad - rightX);
            }
        }
    }
}
