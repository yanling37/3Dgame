using System.Text;
using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Detects NaN/Infinity, logs a structured diagnostic, and prevents propagation.
    /// </summary>
    public static class NumericGuard
    {
        public static bool IsFinite(float value) => !(float.IsNaN(value) || float.IsInfinity(value));

        public static float SanitizeNonNegative(float value, float fallback = 0f)
        {
            if (!IsFinite(value) || value < 0f)
            {
                return fallback;
            }

            return value;
        }

        public static bool AcceptOrHalt(
            WorldState world,
            RegionState region,
            string variable,
            float previousValue,
            float newValue,
            string relevantModifiers)
        {
            if (IsFinite(newValue) && newValue >= 0f)
            {
                return true;
            }

            var sb = new StringBuilder(256);
            sb.AppendLine("[SIMULATION NUMERIC ERROR]");
            sb.AppendLine($"day={world?.DayOfYear ?? -1}");
            sb.AppendLine($"year={world?.Year ?? -1}");
            sb.AppendLine($"totalDays={world?.TotalDays ?? -1}");
            sb.AppendLine($"region={region?.DisplayName ?? region?.Id.ToString() ?? "?"}");
            sb.AppendLine($"variable={variable}");
            sb.AppendLine($"previousValue={previousValue}");
            sb.AppendLine($"newValue={newValue}");
            sb.AppendLine($"relevantModifiers={relevantModifiers}");
            string msg = sb.ToString();
            Debug.LogError(msg);

            if (world != null)
            {
                world.HaltedOnNumericError = true;
                world.LastNumericError = msg;
            }

            return false;
        }
    }
}
