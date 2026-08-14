using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;
using DivineWorld.Simulation.Systems;

namespace HeadlessSimTests
{
    /// <summary>
    /// P2-B v0.4: ObservationSnapshot → History → Trend / Event markers.
    /// Does not modify P2-A simulation math.
    /// </summary>
    public static class Phase2BHistoryTests
    {
        static readonly int[] Checkpoints = { 0, 1, 30, 90, 180, 270, 360 };
        static readonly RegionId[] AllRegions = { RegionId.Theocracy, RegionId.Empire, RegionId.Sea };

        static readonly (string RelPath, string Sha256)[] FrozenFiles =
        {
            ("Assets/Scripts/Simulation/Systems/PopulationSystem.cs", "22629ca5a345ea74663ea8b5eba802755f4e20caa58774bc718e706dffbef24e"),
            ("Assets/Scripts/Simulation/Systems/ResourceSystem.cs", "c72b3859842fc8a507b70af7c05de2f9e9ee7ba52c284f0fe384dfb4896d41ad"),
            ("Assets/Scripts/Simulation/Systems/SeasonSystem.cs", "f43c29e6eeabcf92aa5be676827df96dffa9ef502b71bd30ad048b99d087317d"),
            ("Assets/Scripts/Simulation/Systems/WeatherSystem.cs", "a3d5dbd5dbb04420ea5ab22cba72b197c1176097d913e462e1ba04c22636ecc6"),
            ("Assets/Scripts/Simulation/Systems/EventSystem.cs", "73f7d666d97b8f66404e15a983fc606b1933eda9424535f38bc5bc5d5c26042e"),
            ("Assets/Scripts/Simulation/Systems/FastForwardSystem.cs", "5582066179bbada5c848144321d125659f08cd7950913d2338d3401d1e986a1c"),
            ("Assets/Scripts/Simulation/Systems/SocietySystem.cs", "e028240568c167ff94a2f569f9dfd8bba3392b393057382b094769f31ab7560b"),
            ("Assets/Scripts/Simulation/Data/SimulationConfig.cs", "ee2df4df1e2329fd7bbabdf1edaf5f5d56a2eda02b735acc40289eb4068ef534"),
            ("Assets/Scripts/Simulation/Core/DailySimulation.cs", "fef60ce72032d94a69ea52510ca4c824afa7f275322b14b27a01f0a5c5de3551"),
        };

        public static int Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("Divine World P2-B v0.4 History / Trend Observation");
            Console.WriteLine("=================================================");

            var failures = new List<string>();
            Run("HudTitle_Is_v0.4", TestHudTitle, failures);
            Run("HistoryCapture_RecordsDailyTicks", TestHistoryCapture, failures);
            Run("HistoryCapture_CheckpointsQueryable", TestCheckpoints, failures);
            Run("RegionIsolation", TestRegionIsolation, failures);
            Run("MetricSelection_RefreshesSeries", TestMetricSelection, failures);
            Run("TimeRangeSelection_RefreshesSeries", TestTimeRangeSelection, failures);
            Run("TimeRange_UsesAvailableHistory", TestTimeRangeAvailable, failures);
            Run("EventMarker_RegionIsolation", TestEventMarkerIsolation, failures);
            Run("EventMarker_RemainsAfterExpiry", TestEventRemainsAfterExpiry, failures);
            Run("Reset_ClearsHistory", TestReset, failures);
            Run("FastForward_RecordsPostState", TestFastForward, failures);
            Run("FastForward_NoDuplicateTotalDays", TestFastForwardNoDuplicates, failures);
            Run("FastForward_720Days_NoAnomaly", TestFastForward720, failures);
            Run("SnapshotConsistency_AtCapture", TestSnapshotConsistency, failures);
            Run("SnapshotConsistency_PastUnchanged", TestPastHistoryUnchanged, failures);
            Run("ChartX_UsesTotalDaysNotIndex", TestChartXUsesTotalDays, failures);
            Run("NoNaNInfinityOrNegative", TestNoNanInfinityNegative, failures);
            Run("HistoryLongRun_360", TestLongRun360, failures);
            Run("HistoryLongRun_3600", TestLongRun3600, failures);
            Run("HistoryLongRun_100Years", TestLongRun100Years, failures);
            Run("Query_DoesNotMutateBuffer", TestQueryDoesNotMutate, failures);
            Run("P2A_Freeze_HashesAndNoMutation", TestP2AFreeze, failures);
            Run("ProjectSettings_And_Packages_Unchanged", TestProjectSettingsAndPackagesUnchanged, failures);

            Console.WriteLine();
            const int total = 23;
            Console.WriteLine($"Result: {total - failures.Count}/{total} passed");
            foreach (var f in failures)
            {
                Console.WriteLine("FAIL: " + f);
            }

            if (failures.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("P2-B v0.4 AUTOMATED TEST = PASS");
            }

            return failures.Count == 0 ? 0 : 1;
        }

        static void Run(string name, Action test, List<string> failures)
        {
            try
            {
                test();
                Console.WriteLine("PASS " + name);
            }
            catch (Exception ex)
            {
                failures.Add(name + " :: " + ex.Message);
                Console.WriteLine("FAIL " + name + " :: " + ex.Message);
            }
        }

        static void AssertTrue(bool condition, string message)
        {
            if (!condition)
            {
                throw new Exception(message);
            }
        }

        static void TestHudTitle()
        {
            AssertTrue(ObservationVersion.HudTitle == "P2-B · Observation v0.4", ObservationVersion.HudTitle);
            AssertTrue(ObservationVersion.Number == "v0.4", ObservationVersion.Number);
        }

        static void TestHistoryCapture()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            session.Capture(world.State);
            AssertTrue(session.History.Count(RegionId.Theocracy) == 1, "day 0 sample");

            world.AdvanceDay();
            session.Capture(world.State);
            AssertTrue(session.History.Count(RegionId.Empire) == 2, "tick capture");
            AssertTrue(session.History.Find(RegionId.Sea, world.State.TotalDays) != null, "sea tick present");

            int before = session.History.Count(RegionId.Theocracy);
            session.Capture(world.State);
            AssertTrue(session.History.Count(RegionId.Theocracy) == before, "same TotalDays must not duplicate");
        }

        static void TestCheckpoints()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            session.Capture(world.State);
            AssertAllRegionsAtCurrentDay(session, world, 0);

            int next = 1;
            for (int day = 1; day <= 360; day++)
            {
                world.AdvanceDay();
                session.Capture(world.State);
                if (next < Checkpoints.Length && day == Checkpoints[next])
                {
                    AssertAllRegionsAtCurrentDay(session, world, day);
                    next++;
                }
            }

            AssertTrue(next == Checkpoints.Length, "all checkpoints visited");
            int count = session.History.Count(RegionId.Theocracy);
            AssertTrue(count == 361, "day0+360 ticks, got " + count);
            AssertTrue(session.History.Count(RegionId.Empire) == 361, "empire ticks");
            AssertTrue(session.History.Count(RegionId.Sea) == 361, "sea ticks");
        }

        static void AssertAllRegionsAtCurrentDay(ObservationSession session, HeadlessWorld world, int day)
        {
            for (int r = 0; r < AllRegions.Length; r++)
            {
                AssertCheckpoint(session, world, day, AllRegions[r]);
            }
        }

        static void AssertCheckpoint(ObservationSession session, HeadlessWorld world, int day, RegionId regionId)
        {
            var hist = session.History.Find(regionId, world.State.TotalDays);
            AssertTrue(hist != null, "missing history at day " + day + " " + regionId);
            var snap = session.Current.Find(regionId);
            var live = world.Region(regionId);
            AssertSameMetrics(hist.Region, snap, live, "day " + day + " " + regionId);
            if (regionId == RegionId.Empire)
            {
                Console.WriteLine(
                    "  day " + day
                    + " " + hist.TimeLabel
                    + " Emp pop=" + hist.Region.Population.ToString("0")
                    + " food=" + hist.Region.Food.ToString("0")
                    + " water=" + hist.Region.Water.ToString("0")
                    + " dis=" + hist.Region.Disease.ToString("0.00")
                    + " sta=" + hist.Region.Stability.ToString("0.00")
                    + " mag=" + hist.Region.Magic.ToString("0"));
            }
        }

        static void TestRegionIsolation()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            session.Capture(world.State);

            float theo = session.History.Find(RegionId.Theocracy, 0).Region.Population;
            float emp = session.History.Find(RegionId.Empire, 0).Region.Population;
            float sea = session.History.Find(RegionId.Sea, 0).Region.Population;
            AssertTrue(theo != emp && emp != sea && theo != sea, "baseline pops differ");

            world.Region(RegionId.Empire).Population = 12345f;
            world.AdvanceDay();
            session.Capture(world.State);

            var theoSeries = session.History.Query(RegionId.Theocracy, HistoryMetric.Population, HistoryTimeRange.AllHistory);
            var empSeries = session.History.Query(RegionId.Empire, HistoryMetric.Population, HistoryTimeRange.AllHistory);
            var seaSeries = session.History.Query(RegionId.Sea, HistoryMetric.Population, HistoryTimeRange.AllHistory);

            AssertTrue(theoSeries.Last.Region.Population != 12345f, "theocracy must not receive empire pop");
            AssertTrue(seaSeries.Last.Region.Population != 12345f, "sea must not receive empire pop");
            AssertTrue(empSeries.Last.Region.Population == world.Region(RegionId.Empire).Population,
                "empire series follows empire snapshot");
            AssertTrue(theoSeries.RegionId == RegionId.Theocracy, "theocracy series id");
            AssertTrue(empSeries.RegionId == RegionId.Empire, "empire series id");
        }

        static void TestMetricSelection()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 30);

            var pop = session.History.Query(RegionId.Theocracy, HistoryMetric.Population, HistoryTimeRange.AllHistory);
            var food = session.History.Query(RegionId.Theocracy, HistoryMetric.Food, HistoryTimeRange.AllHistory);
            var water = session.History.Query(RegionId.Theocracy, HistoryMetric.Water, HistoryTimeRange.AllHistory);
            var dis = session.History.Query(RegionId.Theocracy, HistoryMetric.Disease, HistoryTimeRange.AllHistory);
            var sta = session.History.Query(RegionId.Theocracy, HistoryMetric.Stability, HistoryTimeRange.AllHistory);
            var mag = session.History.Query(RegionId.Theocracy, HistoryMetric.Magic, HistoryTimeRange.AllHistory);

            AssertTrue(pop.Metric == HistoryMetric.Population && food.Metric == HistoryMetric.Food, "metric identity");
            AssertTrue(pop.Last.Read(HistoryMetric.Population) == pop.Last.Region.Population, "pop reads snapshot");
            AssertTrue(food.Last.Read(HistoryMetric.Food) == food.Last.Region.Food, "food reads snapshot");
            AssertTrue(water.Last.Read(HistoryMetric.Water) == water.Last.Region.Water, "water");
            AssertTrue(dis.Last.Read(HistoryMetric.Disease) == dis.Last.Region.Disease, "disease");
            AssertTrue(sta.Last.Read(HistoryMetric.Stability) == sta.Last.Region.Stability, "stability");
            AssertTrue(mag.Last.Read(HistoryMetric.Magic) == mag.Last.Region.Magic, "magic");
            AssertTrue(pop.Last.Region.Population != pop.Last.Region.Food, "population series is not food");
            AssertTrue(pop.PlotPoints[pop.PlotPoints.Length - 1].Value == pop.Last.Region.Population, "pop plot uses population");
            AssertTrue(food.PlotPoints[food.PlotPoints.Length - 1].Value == food.Last.Region.Food, "food plot uses food");
            AssertTrue(pop.PlotPoints[pop.PlotPoints.Length - 1].Value != food.PlotPoints[food.PlotPoints.Length - 1].Value,
                "population chart must not show food values");
        }

        static void TestTimeRangeSelection()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 360);

            var all = session.History.Query(RegionId.Sea, HistoryMetric.Water, HistoryTimeRange.AllHistory);
            var y1 = session.History.Query(RegionId.Sea, HistoryMetric.Water, HistoryTimeRange.Recent1Year);
            var d90 = session.History.Query(RegionId.Sea, HistoryMetric.Water, HistoryTimeRange.Recent90Days);
            var d30 = session.History.Query(RegionId.Sea, HistoryMetric.Water, HistoryTimeRange.Recent30Days);

            AssertTrue(all.SampleCount == 361, "all samples " + all.SampleCount);
            AssertTrue(d30.SampleCount == 30, "recent 30 got " + d30.SampleCount);
            AssertTrue(d90.SampleCount == 90, "recent 90 got " + d90.SampleCount);
            AssertTrue(y1.SampleCount == 360, "recent 1y got " + y1.SampleCount);
            AssertTrue(d30.LastTotalDays == all.LastTotalDays, "ranges share latest tick");
            AssertTrue(d30.FirstTotalDays > d90.FirstTotalDays, "30 starts later than 90");
            AssertTrue(d90.FirstTotalDays > y1.FirstTotalDays, "90 starts later than 1y");
            AssertTrue(d30.RequestedRange == HistoryTimeRange.Recent30Days, "range identity");
            AssertMonotonicTotalDays(all);
            AssertMonotonicTotalDays(d30);
            AssertRangeWithinWindow(d30, 30);
            AssertRangeWithinWindow(d90, 90);
            AssertRangeWithinWindow(y1, 360);
        }

        static void AssertMonotonicTotalDays(TrendSeries series)
        {
            for (int i = 1; i < series.Samples.Length; i++)
            {
                AssertTrue(series.Samples[i].TotalDays > series.Samples[i - 1].TotalDays,
                    "time order broken at " + series.Samples[i].TotalDays);
            }
        }

        static void AssertRangeWithinWindow(TrendSeries series, int windowDays)
        {
            int latest = series.LastTotalDays;
            int earliestAllowed = latest - windowDays + 1;
            AssertTrue(series.FirstTotalDays >= earliestAllowed,
                series.RequestedRange + " read before window start " + series.FirstTotalDays + " < " + earliestAllowed);
            for (int i = 0; i < series.Samples.Length; i++)
            {
                AssertTrue(series.Samples[i].TotalDays >= earliestAllowed
                    && series.Samples[i].TotalDays <= latest,
                    "sample outside requested range: " + series.Samples[i].TotalDays);
            }
        }

        static void TestTimeRangeAvailable()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 10);

            var recent90 = session.History.Query(RegionId.Empire, HistoryMetric.Food, HistoryTimeRange.Recent90Days);
            AssertTrue(recent90.SampleCount == 11, "only available days, got " + recent90.SampleCount);
            AssertTrue(recent90.FirstTotalDays == 0, "must not invent days before history start");
            AssertTrue(recent90.ActualRangeLabel.Contains("11 samples"), recent90.ActualRangeLabel);
        }

        static void TestEventMarkerIsolation()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            session.Capture(world.State);
            InjectEvent(world, RegionId.Empire, SimEventType.NaturalDisaster, "emp_disaster");
            InjectEvent(world, RegionId.Theocracy, SimEventType.FoodShortage, "theo_food");
            session.Capture(world.State);

            var emp = session.History.Query(RegionId.Empire, HistoryMetric.Population, HistoryTimeRange.AllHistory);
            var theo = session.History.Query(RegionId.Theocracy, HistoryMetric.Population, HistoryTimeRange.AllHistory);
            var sea = session.History.Query(RegionId.Sea, HistoryMetric.Population, HistoryTimeRange.AllHistory);

            AssertTrue(HasEvent(emp, SimEventType.NaturalDisaster, RegionId.Empire), "empire disaster visible");
            AssertTrue(!HasEvent(emp, SimEventType.FoodShortage, RegionId.Theocracy), "theocracy food must not appear on empire");
            AssertTrue(HasEvent(theo, SimEventType.FoodShortage, RegionId.Theocracy), "theocracy food visible");
            AssertTrue(!HasEvent(theo, SimEventType.NaturalDisaster, RegionId.Empire), "empire disaster must not appear on theocracy");
            AssertTrue(sea.EventMarkers.Length == 0, "sea must not inherit other region events");
        }

        static void TestEventRemainsAfterExpiry()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            InjectEvent(world, RegionId.Sea, SimEventType.DiseaseOutbreak, "sea_disease", startDay: 0, duration: 1);
            session.Capture(world.State);
            AssertTrue(HasEvent(
                session.History.Query(RegionId.Sea, HistoryMetric.Disease, HistoryTimeRange.AllHistory),
                SimEventType.DiseaseOutbreak,
                RegionId.Sea), "captured while active");

            world.AdvanceDays(20);
            session.Capture(world.State);
            var sea = session.History.Query(RegionId.Sea, HistoryMetric.Disease, HistoryTimeRange.AllHistory);
            AssertTrue(HasEvent(sea, SimEventType.DiseaseOutbreak, RegionId.Sea), "marker remains after event ended");
            AssertTrue(!HasEvent(
                session.History.Query(RegionId.Empire, HistoryMetric.Disease, HistoryTimeRange.AllHistory),
                SimEventType.DiseaseOutbreak,
                RegionId.Sea), "ended sea event still not on empire");
        }

        static void TestReset()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 90);
            AssertTrue(session.History.Count(RegionId.Theocracy) == 91, "pre-reset count");
            float oldEmp = session.History.Find(RegionId.Empire, 90).Region.Population;

            world.Reset();
            session.Capture(world.State);

            AssertTrue(session.History.Count(RegionId.Theocracy) == 1, "history cleared to day 0");
            AssertTrue(session.History.Find(RegionId.Empire, 90) == null, "Find/exact query: pre-reset day 90 must not remain");
            AssertTrue(session.History.Find(RegionId.Theocracy, 30) == null, "Find/exact query: old day 30 gone");
            AssertTrue(session.History.Find(RegionId.Sea, 1) == null, "Find/exact query: old day 1 gone");
            AssertTrue(session.History.Find(RegionId.Empire, 0) != null, "new day 0 present");
            AssertTrue(session.History.Find(RegionId.Empire, 0).Region.Population == world.Region(RegionId.Empire).Population,
                "reset history matches new state");
            AssertTrue(session.History.Find(RegionId.Theocracy, 0).Region.Population != oldEmp
                || world.Region(RegionId.Theocracy).Population != oldEmp,
                "new run is a fresh capture");
        }

        static void TestFastForward()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            session.Capture(world.State);
            var before = session.Current;
            float beforePop = before.Find(RegionId.Empire).Population;

            var ff = FastForwardSystem.FastForwardYears(world.State, world.Races, world.Config, 1);
            session.Capture(ff.State);

            var after = session.History.Find(RegionId.Empire, ff.State.TotalDays);
            AssertTrue(after != null, "post-FF history missing");
            var liveEmpire = RegionLookup.FindRegion(ff.State.Regions, RegionId.Empire);
            AssertTrue(after.Region.Population == liveEmpire.Population,
                "FF history copies new snapshot");
            AssertTrue(session.History.Find(RegionId.Empire, 0).Region.Population == beforePop,
                "pre-FF sample retained");
            AssertTrue(after.TotalDays == ff.State.TotalDays, "FF total days");
            AssertTrue(after.TotalDays != before.TotalDays, "must not keep only pre-FF clock");
            AssertSameMetrics(
                after.Region,
                ObservationCapture.FromWorld(ff.State).Find(RegionId.Empire),
                RegionLookup.FindRegion(ff.State.Regions, RegionId.Empire),
                "FF consistency");
        }

        static void TestFastForwardNoDuplicates()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            session.Capture(world.State);
            session.Capture(world.State);
            AssertTrue(session.History.Count(RegionId.Sea) == 1, "duplicate day 0");

            var ff = FastForwardSystem.FastForwardYears(world.State, world.Races, world.Config, 1);
            session.Capture(ff.State);
            session.Capture(ff.State);
            AssertTrue(session.History.Count(RegionId.Sea) == 2, "day0 + post FF, got " + session.History.Count(RegionId.Sea));
            AssertTrue(session.History.Find(RegionId.Sea, 0) != null, "kept origin");
            AssertTrue(session.History.Find(RegionId.Sea, ff.State.TotalDays) != null, "kept FF end");
        }

        static void TestFastForward720()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            world.Reset();
            session.Capture(world.State);

            var ff360 = FastForwardSystem.FastForwardYears(world.State, world.Races, world.Config, 1);
            session.Capture(ff360.State);
            AssertTrue(ff360.State.TotalDays == 360, "360-day FF clock " + ff360.State.TotalDays);
            AssertTrue(session.History.Count(RegionId.Empire) == 2, "day0 + 360");
            AssertSameMetrics(
                session.History.Find(RegionId.Empire, 360).Region,
                ObservationCapture.FromWorld(ff360.State).Find(RegionId.Empire),
                RegionLookup.FindRegion(ff360.State.Regions, RegionId.Empire),
                "FF360");

            var ff720 = FastForwardSystem.FastForwardYears(ff360.State, world.Races, world.Config, 1);
            session.Capture(ff720.State);
            AssertTrue(ff720.State.TotalDays == 720, "720-day FF clock " + ff720.State.TotalDays);
            AssertTrue(session.History.Count(RegionId.Sea) == 3, "day0 + 360 + 720, got " + session.History.Count(RegionId.Sea));
            AssertTrue(session.History.Find(RegionId.Theocracy, 0) != null, "kept origin");
            AssertTrue(session.History.Find(RegionId.Theocracy, 360) != null, "kept 360");
            AssertTrue(session.History.Find(RegionId.Theocracy, 720) != null, "kept 720");
            AssertTrue(session.History.Find(RegionId.Empire, 360).TotalDays
                < session.History.Find(RegionId.Empire, 720).TotalDays, "time order");

            var series = session.History.Query(RegionId.Empire, HistoryMetric.Population, HistoryTimeRange.AllHistory);
            AssertMonotonicTotalDays(series);
            AssertSameMetrics(
                session.History.Find(RegionId.Sea, 720).Region,
                ObservationCapture.FromWorld(ff720.State).Find(RegionId.Sea),
                RegionLookup.FindRegion(ff720.State.Regions, RegionId.Sea),
                "FF720");

            float pop0 = session.History.Find(RegionId.Empire, 0).Region.Population;
            float pop720 = session.History.Find(RegionId.Empire, 720).Region.Population;
            AssertTrue(pop720 < pop0 * 1000f, "FF720 population exploded " + pop720);
            string reason;
            AssertTrue(!session.History.HasNonFiniteOrNegative(out reason), reason ?? "FF720 bad values");
        }

        static void TestSnapshotConsistency()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 45);
            var snap = session.Current;
            foreach (var region in world.State.Regions)
            {
                var observed = snap.Find(region.Id);
                var hist = session.History.Find(region.Id, snap.TotalDays);
                AssertSameMetrics(hist.Region, observed, region, region.Id.ToString());
            }
        }

        static void TestPastHistoryUnchanged()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 30);
            var day30 = session.History.Find(RegionId.Theocracy, world.State.TotalDays);
            float pop = day30.Region.Population;
            float food = day30.Region.Food;
            float water = day30.Region.Water;
            float dis = day30.Region.Disease;
            float sta = day30.Region.Stability;
            float mag = day30.Region.Magic;
            int day = day30.TotalDays;

            RunDays(world, session, 60);
            var past = session.History.Find(RegionId.Theocracy, day);
            AssertTrue(past.Region.Population == pop, "past population mutated");
            AssertTrue(past.Region.Food == food, "past food mutated");
            AssertTrue(past.Region.Water == water, "past water mutated");
            AssertTrue(past.Region.Disease == dis, "past disease mutated");
            AssertTrue(past.Region.Stability == sta, "past stability mutated");
            AssertTrue(past.Region.Magic == mag, "past magic mutated");
        }

        static void TestChartXUsesTotalDays()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            session.Capture(world.State);
            world.AdvanceDays(30);
            session.Capture(world.State);
            var ff = FastForwardSystem.FastForwardToTotalDay(world.State, world.Races, world.Config, 360);
            session.Capture(ff.State);

            var series = session.History.Query(RegionId.Empire, HistoryMetric.Population, HistoryTimeRange.AllHistory);
            AssertTrue(series.SampleCount == 3, "sparse samples " + series.SampleCount);
            AssertTrue(series.PlotPoints.Length == 3, "plot uses captured ticks");
            AssertTrue(series.AxisLabels.Length >= 2, "axis labels present");
            for (int i = 0; i < series.AxisLabels.Length; i++)
            {
                AssertTrue(series.AxisLabels[i].Text.StartsWith("Year "), "label must be calendar, got " + series.AxisLabels[i].Text);
                AssertTrue(!series.AxisLabels[i].Text.Contains("index"), "must not show array index");
            }

            float x30 = TrendChartGeometry.MapX(30, 0, 360, 0f, 360f);
            float xIndex = 180f;
            AssertTrue(Math.Abs(x30 - 30f) < 0.01f, "X must map TotalDays 30 → 30px, got " + x30);
            AssertTrue(Math.Abs(x30 - xIndex) > 50f, "X must not use array index midpoint");

            int[] marks = { 1, 90, 180, 270, 360 };
            for (int i = 0; i < marks.Length; i++)
            {
                float x = TrendChartGeometry.MapX(marks[i], 0, 360, 0f, 360f);
                AssertTrue(Math.Abs(x - marks[i]) < 0.01f, "X(TotalDays " + marks[i] + ") got " + x);
                if (i > 0)
                {
                    float prev = TrendChartGeometry.MapX(marks[i - 1], 0, 360, 0f, 360f);
                    AssertTrue(x > prev, "timeline must increase with TotalDays");
                }
            }
        }

        static void TestNoNanInfinityNegative()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 360);
            string reason;
            AssertTrue(!session.History.HasNonFiniteOrNegative(out reason), reason ?? "bad history");
        }

        static void TestLongRun360()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 360);
            int entries = session.History.TotalEntryCount();
            AssertTrue(entries == 361 * 3, "unexpected entry count " + entries);
            AssertTrue(entries < 5000, "abnormally large history " + entries);
            string reason;
            AssertTrue(!session.History.HasNonFiniteOrNegative(out reason), reason ?? "bad");
            AssertTrue(!world.State.HaltedOnNumericError, world.State.LastNumericError ?? "halt");
        }

        static void TestLongRun3600()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 3600);
            int perRegion = session.History.Count(RegionId.Theocracy);
            AssertTrue(perRegion == 3601, "3600-day entries " + perRegion);
            AssertTrue(session.History.TotalEntryCount() == 3601 * 3, "3 regions");
            AssertTrue(session.History.Find(RegionId.Empire, 3600) != null, "day 3600 present");
            AssertTrue(session.History.Find(RegionId.Sea, 3599) != null, "no missing last-but-one");
            int prev = -1;
            var all = session.History.Query(RegionId.Empire, HistoryMetric.Water, HistoryTimeRange.AllHistory);
            for (int i = 0; i < all.Samples.Length; i++)
            {
                AssertTrue(all.Samples[i].TotalDays > prev, "duplicate or unordered TotalDays " + all.Samples[i].TotalDays);
                prev = all.Samples[i].TotalDays;
            }

            string reason;
            AssertTrue(!session.History.HasNonFiniteOrNegative(out reason), reason ?? "3600 bad");
            AssertTrue(!world.State.HaltedOnNumericError, world.State.LastNumericError ?? "halt");
            Console.WriteLine("  3600d history entries/region=" + perRegion + " total=" + session.History.TotalEntryCount());
        }

        static void TestLongRun100Years()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            session.Capture(world.State);
            int[] yearMarks = { 1, 30, 90, 180, 270, 360 };
            for (int day = 1; day <= 36000; day++)
            {
                world.AdvanceDay();
                int doy = ((day - 1) % 360) + 1;
                bool checkpoint = false;
                for (int i = 0; i < yearMarks.Length; i++)
                {
                    if (doy == yearMarks[i])
                    {
                        checkpoint = true;
                        break;
                    }
                }

                if (checkpoint || day == 36000)
                {
                    session.Capture(world.State);
                }

                if (world.State.HaltedOnNumericError)
                {
                    throw new Exception(world.State.LastNumericError);
                }
            }

            AssertTrue(session.History.Find(RegionId.Empire, 1) != null, "day 1");
            AssertTrue(session.History.Find(RegionId.Empire, 30) != null, "day 30");
            AssertTrue(session.History.Find(RegionId.Empire, 90) != null, "day 90");
            AssertTrue(session.History.Find(RegionId.Empire, 180) != null, "day 180");
            AssertTrue(session.History.Find(RegionId.Empire, 270) != null, "day 270");
            AssertTrue(session.History.Find(RegionId.Empire, 360) != null, "day 360");
            AssertTrue(session.History.Count(RegionId.Sea) < 80000, "history explosion " + session.History.Count(RegionId.Sea));
            string reason;
            AssertTrue(!session.History.HasNonFiniteOrNegative(out reason), reason ?? "bad 100y");
            Console.WriteLine("  100y history entries/region=" + session.History.Count(RegionId.Theocracy));
        }

        static void TestQueryDoesNotMutate()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 40);
            int before = session.History.Count(RegionId.Empire);
            int rev = session.History.Revision;
            session.History.Query(RegionId.Empire, HistoryMetric.Food, HistoryTimeRange.Recent30Days);
            session.History.Query(RegionId.Sea, HistoryMetric.Magic, HistoryTimeRange.AllHistory);
            AssertTrue(session.History.Count(RegionId.Empire) == before, "query mutated count");
            AssertTrue(session.History.Revision == rev, "query must not bump revision");
        }

        static void TestP2AFreeze()
        {
            string root = FindRepoRoot();
            AssertTrue(root != null, "repo root");
            foreach (var file in FrozenFiles)
            {
                string path = Path.Combine(root, file.RelPath);
                AssertTrue(File.Exists(path), "missing frozen file " + file.RelPath);
                string hash = Sha256File(path);
                AssertTrue(hash == file.Sha256,
                    file.RelPath + " hash changed (P2-A freeze broken). expected " + file.Sha256 + " got " + hash);
            }

            var world = new HeadlessWorld();
            float[] pops = CopyPops(world);
            var session = new ObservationSession();
            session.Capture(world.State);
            session.History.Query(RegionId.Empire, HistoryMetric.Population, HistoryTimeRange.AllHistory);
            float[] after = CopyPops(world);
            for (int i = 0; i < pops.Length; i++)
            {
                AssertTrue(pops[i] == after[i], "history mutated simulation population");
            }

            string freezeDiff = GitDiffNames(root, "main",
                "Assets/Scripts/Simulation/Systems/PopulationSystem.cs",
                "Assets/Scripts/Simulation/Systems/ResourceSystem.cs",
                "Assets/Scripts/Simulation/Systems/SeasonSystem.cs",
                "Assets/Scripts/Simulation/Systems/WeatherSystem.cs",
                "Assets/Scripts/Simulation/Systems/EventSystem.cs",
                "Assets/Scripts/Simulation/Systems/FastForwardSystem.cs",
                "Assets/Scripts/Simulation/Systems/SocietySystem.cs",
                "Assets/Scripts/Simulation/Data/SimulationConfig.cs",
                "Assets/Scripts/Simulation/Core/DailySimulation.cs");
            AssertTrue(string.IsNullOrWhiteSpace(freezeDiff), "P2-A frozen files differ from main:\n" + freezeDiff);

            string capture = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Simulation/Observation/ObservationCapture.cs"));
            string history = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Simulation/Observation/ObservationHistory.cs"));
            string hud = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Simulation/UI/HistoryTrendHud.cs"));
            AssertTrue(!capture.Contains("PopulationSystem"), "capture must not call PopulationSystem");
            AssertTrue(!history.Contains("PopulationSystem"), "history must not call PopulationSystem");
            AssertTrue(!history.Contains("DailySimulation"), "history must not tick simulation");
            AssertTrue(!hud.Contains("new GameObject"), "trend HUD must not create chart GameObjects");
        }

        static void TestProjectSettingsAndPackagesUnchanged()
        {
            string root = FindRepoRoot();
            AssertTrue(root != null, "repo root");
            var hashed = new (string RelPath, string Sha256)[]
            {
                ("ProjectSettings/ProjectVersion.txt", "b42279cfd794d9f1825f3b7c1f318b861fa9e2e2b3c6c146737bdbd41c01b389"),
                ("Packages/manifest.json", "bb54c36c1d185581b77af229153f17b3a42faf1498e18f45cde99c981f103625"),
                ("Packages/packages-lock.json", "8f8da263666198014ea3aab3d3faae02ceae10d64f319e74874084b3585cb022")
            };
            foreach (var file in hashed)
            {
                string path = Path.Combine(root, file.RelPath);
                AssertTrue(File.Exists(path), "missing " + file.RelPath);
                string hash = Sha256File(path);
                AssertTrue(hash == file.Sha256, file.RelPath + " changed. expected " + file.Sha256 + " got " + hash);
            }

            string version = File.ReadAllText(Path.Combine(root, "ProjectSettings/ProjectVersion.txt"));
            AssertTrue(version.Contains("2022.3.62f3c1"), "Unity version must stay 2022.3.62f3c1");
            AssertTrue(version.Contains("1623fc0bbb97"), "Unity revision must stay 1623fc0bbb97");

            string diff = GitDiffNames(root, "main", "ProjectSettings", "Packages");
            AssertTrue(string.IsNullOrWhiteSpace(diff), "ProjectSettings/Packages differ from main:\n" + diff);
        }

        static void AssertSameMetrics(
            RegionObservationSnapshot hist,
            RegionObservationSnapshot snap,
            RegionState live,
            string label)
        {
            AssertTrue(hist != null && snap != null && live != null, "missing " + label);
            AssertTrue(hist.Population == snap.Population && snap.Population == live.Population,
                label + " population hist=" + hist.Population + " snap=" + snap.Population + " live=" + live.Population);
            AssertTrue(hist.Food == snap.Food && snap.Food == live.Get(ResourceId.Food), label + " food");
            AssertTrue(hist.Water == snap.Water && snap.Water == live.Get(ResourceId.Water), label + " water");
            AssertTrue(hist.Disease == snap.Disease && snap.Disease == live.DiseasePressure, label + " disease");
            AssertTrue(hist.Stability == snap.Stability && snap.Stability == live.Stability, label + " stability");
            AssertTrue(hist.Magic == snap.Magic && snap.Magic == live.Get(ResourceId.Magic), label + " magic");
            AssertTrue(hist.Wood == live.Get(ResourceId.Timber), label + " wood");
            AssertTrue(hist.Mineral == live.Get(ResourceId.Ore), label + " mineral");
            AssertTrue(hist.Education == live.Education, label + " education");
            AssertTrue(hist.Faith == live.FaithLevel, label + " faith");
            AssertTrue(hist.LastCarryingCapacity == live.LastCarryingCapacity, label + " carrying");
        }

        static void RunDays(HeadlessWorld world, ObservationSession session, int days)
        {
            if (session.History.Count(RegionId.Theocracy) == 0)
            {
                session.Capture(world.State);
            }

            for (int i = 0; i < days; i++)
            {
                world.AdvanceDay();
                session.Capture(world.State);
                if (world.State.HaltedOnNumericError)
                {
                    throw new Exception(world.State.LastNumericError);
                }
            }
        }

        static void InjectEvent(HeadlessWorld world, RegionId regionId, SimEventType type, string eventId, int startDay = -1, int duration = 8)
        {
            var region = world.Region(regionId);
            if (region.ActiveEvents == null)
            {
                region.ActiveEvents = new List<RegionEvent>();
            }

            region.ActiveEvents.Add(new RegionEvent
            {
                EventId = eventId,
                EventType = type,
                RegionId = regionId,
                Scope = SimEventScope.Regional,
                StartDay = startDay >= 0 ? startDay : world.State.TotalDays,
                Duration = duration,
                Severity = 1f
            });
        }

        static bool HasEvent(TrendSeries series, SimEventType type, RegionId region)
        {
            for (int i = 0; i < series.EventMarkers.Length; i++)
            {
                if (series.EventMarkers[i].EventType == type && series.EventMarkers[i].RegionId == region)
                {
                    return true;
                }
            }

            return false;
        }

        static float[] CopyPops(HeadlessWorld world)
        {
            var pops = new float[world.State.Regions.Length];
            for (int i = 0; i < pops.Length; i++)
            {
                pops[i] = world.State.Regions[i].Population;
            }

            return pops;
        }

        static string Sha256File(string path)
        {
            byte[] raw = File.ReadAllBytes(path);
            using (var ms = new MemoryStream(raw.Length))
            {
                for (int i = 0; i < raw.Length; i++)
                {
                    if (raw[i] == (byte)'\r' && i + 1 < raw.Length && raw[i + 1] == (byte)'\n')
                    {
                        continue;
                    }

                    ms.WriteByte(raw[i]);
                }

                using (var sha = SHA256.Create())
                {
                    byte[] hash = sha.ComputeHash(ms.ToArray());
                    var sb = new StringBuilder(hash.Length * 2);
                    for (int i = 0; i < hash.Length; i++)
                    {
                        sb.Append(hash[i].ToString("x2"));
                    }

                    return sb.ToString();
                }
            }
        }

        static string GitDiffNames(string root, string baseline, params string[] paths)
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "diff --name-only " + baseline + " -- " + string.Join(" ", paths),
                WorkingDirectory = root,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using (var proc = System.Diagnostics.Process.Start(psi))
            {
                AssertTrue(proc != null, "failed to start git");
                string output = proc.StandardOutput.ReadToEnd();
                string err = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                AssertTrue(proc.ExitCode == 0, "git diff failed: " + err);
                return output.Trim();
            }
        }

        static string FindRepoRoot()
        {
            var starts = new[]
            {
                Directory.GetCurrentDirectory(),
                AppContext.BaseDirectory
            };

            foreach (var start in starts)
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    string candidate = Path.Combine(dir.FullName, FrozenFiles[0].RelPath);
                    if (File.Exists(candidate))
                    {
                        return dir.FullName;
                    }

                    dir = dir.Parent;
                }
            }

            return null;
        }
    }
}
