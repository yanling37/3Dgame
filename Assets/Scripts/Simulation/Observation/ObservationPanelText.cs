using System.Globalization;
using System.Text;
using DivineWorld.Simulation.Data;

namespace DivineWorld.Simulation.Observation
{
    /// <summary>
    /// Formats observation snapshots for HUD / tests. Reads snapshot fields only.
    /// </summary>
    public static class ObservationPanelText
    {
        public static string FormatWorldHeader(WorldObservationSnapshot world)
        {
            if (world == null)
            {
                return ObservationLabels.UiVersion;
            }

            int daysPerYear = world.DaysPerYear > 0 ? world.DaysPerYear : SimulationConfig.DaysPerYear;
            int daysPerSeason = world.DaysPerSeason > 0 ? world.DaysPerSeason : SimulationConfig.DaysPerSeason;
            var sb = new StringBuilder();
            sb.AppendLine(ObservationLabels.UiVersion);
            sb.AppendLine($"Year {world.Year}");
            sb.AppendLine($"Day {world.DayOfYear} / {daysPerYear}");
            sb.AppendLine($"Season {ObservationLabels.SeasonName(world.CurrentSeason)}");
            sb.Append($"季内第 {world.DayInSeason} / {daysPerSeason} 日");
            return sb.ToString();
        }

        public static string FormatRegionPanel(WorldObservationSnapshot world, RegionObservationSnapshot region)
        {
            if (region == null)
            {
                return "未选中地区";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"地区名称  {region.DisplayName}");
            sb.AppendLine($"人口      {F0(region.Population)}");
            sb.AppendLine($"人口变化  {Signed(region.PopulationDelta)}");
            sb.AppendLine($"承载力    {F0(region.CarryingCapacity)}");
            sb.AppendLine($"粮食      {F0(region.Food)}");
            sb.AppendLine($"水        {F0(region.Water)}");
            sb.AppendLine($"木        {F0(region.Timber)}");
            sb.AppendLine($"矿        {F0(region.Ore)}");
            sb.AppendLine($"魔力      {F0(region.Mana)}");
            sb.AppendLine($"疫病      {F2(region.DiseasePressure)}");
            sb.AppendLine($"稳定      {F2(region.Stability)}");
            sb.AppendLine($"教育      {F2(region.Education)}");
            sb.AppendLine($"信仰      {F2(region.Faith)}");
            if (world != null)
            {
                int daysPerSeason = world.DaysPerSeason > 0 ? world.DaysPerSeason : SimulationConfig.DaysPerSeason;
                sb.AppendLine($"当前季节  {ObservationLabels.SeasonName(world.CurrentSeason)}  (季内第 {world.DayInSeason} / {daysPerSeason} 日)");
            }

            sb.AppendLine("当前事件");
            sb.Append(FormatEvents(region));
            return sb.ToString();
        }

        public static string FormatEvents(RegionObservationSnapshot region)
        {
            if (region?.ActiveEvents == null || region.ActiveEvents.Length == 0)
            {
                return ObservationLabels.NoEvent;
            }

            var sb = new StringBuilder();
            int shown = 0;
            for (int i = 0; i < region.ActiveEvents.Length; i++)
            {
                var e = region.ActiveEvents[i];
                if (e == null || !e.IsActive)
                {
                    continue;
                }

                if (shown > 0)
                {
                    sb.AppendLine();
                }

                sb.AppendLine($"  名称      {e.DisplayName}");
                sb.AppendLine($"  严重程度  {F2(e.Severity)}");
                sb.AppendLine($"  开始时间  累计第 {e.StartDay} 日");
                sb.AppendLine($"  结束时间  累计第 {e.EndDay} 日");
                sb.Append($"  剩余时间  {e.RemainingDays} 日");
                shown++;
            }

            return shown == 0 ? ObservationLabels.NoEvent : sb.ToString();
        }

        public static bool RegionHasActiveEvent(RegionObservationSnapshot region)
        {
            return DominantActiveEvent(region) != null;
        }

        /// <summary>Picks the highest-severity active event already present on the snapshot (no re-evaluation).</summary>
        public static EventObservation DominantActiveEvent(RegionObservationSnapshot region)
        {
            if (region?.ActiveEvents == null)
            {
                return null;
            }

            EventObservation best = null;
            for (int i = 0; i < region.ActiveEvents.Length; i++)
            {
                var e = region.ActiveEvents[i];
                if (e == null || !e.IsActive)
                {
                    continue;
                }

                if (best == null || e.Severity >= best.Severity)
                {
                    best = e;
                }
            }

            return best;
        }

        public static bool SnapshotIsFinite(WorldObservationSnapshot world)
        {
            if (world == null)
            {
                return false;
            }

            if (!Finite(world.TotalPopulation) || !Finite(world.TotalFood) || !Finite(world.TotalWater) || !Finite(world.TotalMana))
            {
                return false;
            }

            if (world.Regions == null)
            {
                return true;
            }

            for (int i = 0; i < world.Regions.Length; i++)
            {
                var r = world.Regions[i];
                if (r == null)
                {
                    continue;
                }

                if (!Finite(r.Population) || !Finite(r.PopulationDelta) || !Finite(r.CarryingCapacity)
                    || !Finite(r.Food) || !Finite(r.Water) || !Finite(r.Timber) || !Finite(r.Ore) || !Finite(r.Mana)
                    || !Finite(r.DiseasePressure) || !Finite(r.Stability) || !Finite(r.Education) || !Finite(r.Faith))
                {
                    return false;
                }
            }

            return true;
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        static string F0(float v) => v.ToString("0", CultureInfo.InvariantCulture);

        static string F2(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

        static string Signed(float v)
        {
            string body = v.ToString("0.00", CultureInfo.InvariantCulture);
            return v > 0f ? "+" + body : body;
        }
    }
}
