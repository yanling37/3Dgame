using DivineWorld.Simulation.Data;
using UnityEngine;

namespace DivineWorld.Simulation.Systems
{
    /// <summary>
    /// Season sets weather baseline/range; weather drifts continuously inside that range.
    /// </summary>
    public static class WeatherSystem
    {
        public static void TickDay(RegionState region, SeasonId season, SimulationConfig config, System.Random rng)
        {
            if (region == null || config == null)
            {
                return;
            }

            config.GetWeatherRange(season, out float baseline, out float min, out float max);

            float weather = region.WeatherFactor;
            if (float.IsNaN(weather) || float.IsInfinity(weather))
            {
                weather = baseline;
            }

            float noise = 0f;
            if (rng != null)
            {
                noise = ((float)rng.NextDouble() - 0.5f) * 2f * config.WeatherNoiseAmplitude;
            }

            // Continuous drift toward seasonal baseline, then clamp to seasonal envelope.
            weather += (baseline - weather) * config.WeatherPullToBaseline + noise;
            region.WeatherFactor = Mathf.Clamp(weather, min, max);
        }
    }
}
