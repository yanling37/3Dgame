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
    /// P2-B v0.5: region report, multi-region compare, resource nodes.
    /// Observation layer only — does not modify P2-A math.
    /// </summary>
    public static class Phase2BReportTests
    {
        static readonly RegionId[] AllRegions = { RegionId.Theocracy, RegionId.Empire, RegionId.Sea };

        static readonly HistoryMetric[] CompareMetrics =
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
            Console.WriteLine("Divine World P2-B v0.5 Report / Compare / Resource Nodes");
            Console.WriteLine("=======================================================");

            var failures = new List<string>();
            Run("HudTitle_Is_v0.5", TestHudTitle, failures);
            Run("Report_SnapshotConsistency", TestReportConsistency, failures);
            Run("Report_RegionIsolation", TestReportRegionIsolation, failures);
            Run("Report_PreviousPeriodComparison", TestReportPreviousPeriod, failures);
            Run("Compare_MultiRegionMetric", TestCompareMultiRegion, failures);
            Run("Compare_MetricSelection", TestCompareMetricSelection, failures);
            Run("Compare_SharedTimeRange", TestCompareSharedTimeRange, failures);
            Run("ResourceNode_RegionIsolation", TestResourceNodeIsolation, failures);
            Run("ResourceNode_DataBinding", TestResourceNodeBinding, failures);
            Run("Reset_ClearsReportCompareNodes", TestReset, failures);
            Run("FastForward_UpdatesObservationLayer", TestFastForward, failures);
            Run("NoNaNInfinity", TestNoNanInfinity, failures);
            Run("LongRun_360", TestLongRun360, failures);
            Run("LongRun_3600", TestLongRun3600, failures);
            Run("P2A_Freeze_HashesAndNoMutation", TestP2AFreeze, failures);
            Run("ProjectSettings_And_Packages_Unchanged", TestProjectSettingsAndPackagesUnchanged, failures);

            Console.WriteLine();
            const int total = 16;
            Console.WriteLine($"Result: {total - failures.Count}/{total} passed");
            foreach (var f in failures)
            {
                Console.WriteLine("FAIL: " + f);
            }

            if (failures.Count == 0)
            {
                Console.WriteLine();
                Console.WriteLine("P2-B v0.5 AUTOMATED TEST = PASS");
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
            AssertTrue(ObservationVersion.HudTitle == "P2-B · Observation v0.5", ObservationVersion.HudTitle);
            AssertTrue(ObservationVersion.Number == "v0.5", ObservationVersion.Number);
        }

        static void TestReportConsistency()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 45);
            var report = RegionReportBuilder.Build(session.History, session.Current, RegionId.Empire, ReportPeriod.Season);
            var hist = session.History.Latest(RegionId.Empire);
            var snap = session.Current.Find(RegionId.Empire);
            var live = world.Region(RegionId.Empire);
            AssertTrue(report.RegionId == RegionId.Empire, "report region");
            AssertTrue(report.Title.Contains("Empire") || report.DisplayName.Contains("帝国") || report.DisplayName.Length > 0, report.Title);
            foreach (var metric in RegionReport.Metrics)
            {
                var line = report.Line(metric);
                AssertTrue(line != null, "missing " + metric);
                float fromHist = hist.Read(metric);
                float fromSnap = HistoryMetrics.Read(snap, metric);
                AssertTrue(line.Current == fromHist, metric + " report " + line.Current + " != history " + fromHist);
                AssertTrue(fromHist == fromSnap, metric + " history != snapshot");
            }

            AssertTrue(report.Line(HistoryMetric.Population).Current == live.Population, "pop live");
            AssertTrue(report.Line(HistoryMetric.Food).Current == live.Get(ResourceId.Food), "food live");
            AssertTrue(report.Line(HistoryMetric.Water).Current == live.Get(ResourceId.Water), "water live");
            AssertTrue(report.Line(HistoryMetric.Disease).Current == live.DiseasePressure, "disease live");
            AssertTrue(report.Line(HistoryMetric.Stability).Current == live.Stability, "stability live");
        }

        static void TestReportRegionIsolation()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            session.Capture(world.State);
            InjectEvent(world, RegionId.Empire, SimEventType.NaturalDisaster, "emp_only");
            session.Capture(world.State);

            var emp = RegionReportBuilder.Build(session.History, session.Current, RegionId.Empire, ReportPeriod.Year);
            var theo = RegionReportBuilder.Build(session.History, session.Current, RegionId.Theocracy, ReportPeriod.Year);
            AssertTrue(emp.Line(HistoryMetric.Population).Current != theo.Line(HistoryMetric.Population).Current,
                "reports must not share population");
            AssertTrue(HasReportEvent(emp, SimEventType.NaturalDisaster), "empire event in empire report");
            AssertTrue(!HasReportEvent(theo, SimEventType.NaturalDisaster), "empire event leaked to theocracy report");
        }

        static void TestReportPreviousPeriod()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            session.Capture(world.State);
            var day0 = session.History.Latest(RegionId.Theocracy);
            float pop0 = day0.Region.Population;
            float food0 = day0.Region.Food;

            RunDays(world, session, 360);
            var report = RegionReportBuilder.Build(session.History, session.Current, RegionId.Theocracy, ReportPeriod.Year);
            AssertTrue(report.HasPrevious, "year report should have previous after 360 days");
            var linePop = report.Line(HistoryMetric.Population);
            var lineFood = report.Line(HistoryMetric.Food);
            AssertTrue(linePop.Previous == pop0, "previous pop must be history day 0, got " + linePop.Previous);
            AssertTrue(lineFood.Previous == food0, "previous food must be history day 0");
            float expectedPct = (linePop.Current - pop0) / Math.Abs(pop0) * 100f;
            AssertTrue(Math.Abs(linePop.Percent - expectedPct) < 0.05f, "percent from history, not recomputed");
            AssertTrue(linePop.Trend != ReportTrend.Flat || Math.Abs(linePop.Percent) <= 0.5f, "trend matches percent");
            AssertTrue(report.TurningPoints.Length >= 1, "turning points from history");
            Console.WriteLine("  Year report pop " + linePop.Current.ToString("0") + " " + linePop.TrendMark + " " + linePop.Percent.ToString("0.0") + "%");
        }

        static void TestCompareMultiRegion()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 30);
            var cmp = session.History.QueryCompare(HistoryMetric.Food, HistoryTimeRange.AllHistory, 360);
            AssertTrue(cmp.Series.Length == 3, "three series");
            var theo = cmp.For(RegionId.Theocracy);
            var emp = cmp.For(RegionId.Empire);
            var sea = cmp.For(RegionId.Sea);
            AssertTrue(theo.RegionId == RegionId.Theocracy && emp.RegionId == RegionId.Empire && sea.RegionId == RegionId.Sea, "ids");
            AssertTrue(theo.HasData && emp.HasData && sea.HasData, "all have data");
            AssertTrue(cmp.Current(RegionId.Theocracy) != cmp.Current(RegionId.Empire), "food not merged");
            AssertTrue(cmp.Current(RegionId.Empire) != cmp.Current(RegionId.Sea), "empire/sea distinct");
            AssertTrue(cmp.Current(RegionId.Theocracy) == session.Current.Find(RegionId.Theocracy).Food, "theo food from snapshot");
            AssertTrue(cmp.Current(RegionId.Empire) == session.History.Latest(RegionId.Empire).Region.Food, "emp food from history");
            AssertTrue(cmp.RangeStart == theo.FirstTotalDays && cmp.RangeStart == emp.FirstTotalDays && cmp.RangeStart == sea.FirstTotalDays,
                "shared start");
            AssertTrue(cmp.RangeEnd == theo.LastTotalDays && cmp.RangeEnd == emp.LastTotalDays, "shared end");
        }

        static void TestCompareMetricSelection()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 20);
            float food = session.History.QueryCompare(HistoryMetric.Food, HistoryTimeRange.AllHistory).Current(RegionId.Sea);
            float pop = session.History.QueryCompare(HistoryMetric.Population, HistoryTimeRange.AllHistory).Current(RegionId.Sea);
            float wood = session.History.QueryCompare(HistoryMetric.Wood, HistoryTimeRange.AllHistory).Current(RegionId.Sea);
            AssertTrue(food != pop, "food series is not population");
            AssertTrue(wood == session.Current.Find(RegionId.Sea).Wood, "wood metric reads snapshot wood");
            foreach (var metric in CompareMetrics)
            {
                var cmp = session.History.QueryCompare(metric, HistoryTimeRange.AllHistory);
                AssertTrue(cmp.Metric == metric, "metric identity " + metric);
                AssertTrue(cmp.For(RegionId.Empire).Metric == metric, "series metric " + metric);
            }
        }

        static void TestCompareSharedTimeRange()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 360);
            var d30 = session.History.QueryCompare(HistoryMetric.Water, HistoryTimeRange.Recent30Days);
            var d90 = session.History.QueryCompare(HistoryMetric.Water, HistoryTimeRange.Recent90Days);
            session.History.SharedWindow(HistoryTimeRange.Recent30Days, out int start30, out int end30);
            session.History.SharedWindow(HistoryTimeRange.Recent90Days, out int start90, out int end90);
            AssertTrue(d30.RangeStart == start30 && d30.RangeEnd == end30, "compare uses shared window");
            AssertTrue(d30.For(RegionId.Theocracy).FirstTotalDays == start30, "theo 30");
            AssertTrue(d30.For(RegionId.Empire).FirstTotalDays == start30, "emp 30");
            AssertTrue(d30.For(RegionId.Sea).FirstTotalDays == start30, "sea 30");
            AssertTrue(start30 > start90, "30 starts later than 90");
            AssertTrue(d30.Range == HistoryTimeRange.Recent30Days && d90.Range == HistoryTimeRange.Recent90Days, "range ids");
            AssertTrue(end30 == end90, "same latest tick");
        }

        static void TestResourceNodeIsolation()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            session.Capture(world.State);
            var nodes = new ResourceNodeState();
            nodes.Apply(session.Current);
            AssertTrue(nodes.Count == 15, "3×5 nodes, got " + nodes.Count);
            AssertTrue(nodes.CountFor(RegionId.Theocracy) == 5, "theo 5");
            AssertTrue(nodes.CountFor(RegionId.Empire) == 5, "emp 5");
            AssertTrue(nodes.CountFor(RegionId.Sea) == 5, "sea 5");
            var empFood = nodes.Find(RegionId.Empire, ResourceNodeType.Food);
            var theoFood = nodes.Find(RegionId.Theocracy, ResourceNodeType.Food);
            AssertTrue(empFood.RegionId == RegionId.Empire, "empire food region");
            AssertTrue(theoFood.RegionId == RegionId.Theocracy, "theo food region");
            AssertTrue(empFood.Amount != theoFood.Amount, "food amounts not merged");
            AssertTrue(empFood.Amount == session.Current.Find(RegionId.Empire).Food, "empire food bound");
        }

        static void TestResourceNodeBinding()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            RunDays(world, session, 30);
            var nodes = new ResourceNodeState();
            nodes.Apply(session.Current);
            foreach (var id in AllRegions)
            {
                var snap = session.Current.Find(id);
                var live = world.Region(id);
                AssertTrue(nodes.Find(id, ResourceNodeType.Food).Amount == snap.Food && snap.Food == live.Get(ResourceId.Food), id + " food");
                AssertTrue(nodes.Find(id, ResourceNodeType.Water).Amount == snap.Water, id + " water");
                AssertTrue(nodes.Find(id, ResourceNodeType.Water).Capacity == snap.WaterCapacity, id + " water cap");
                AssertTrue(nodes.Find(id, ResourceNodeType.Water).Capacity == live.LastWaterCapacity, id + " water cap live");
                AssertTrue(nodes.Find(id, ResourceNodeType.Wood).Amount == snap.Wood, id + " wood");
                AssertTrue(nodes.Find(id, ResourceNodeType.Mineral).Amount == snap.Mineral, id + " mineral");
                AssertTrue(nodes.Find(id, ResourceNodeType.Magic).Amount == snap.Magic, id + " magic");
            }
        }

        static void TestReset()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            var nodes = new ResourceNodeState();
            RunDays(world, session, 90);
            nodes.Apply(session.Current);
            float oldFood = nodes.Find(RegionId.Empire, ResourceNodeType.Food).Amount;
            var oldReport = RegionReportBuilder.Build(session.History, session.Current, RegionId.Empire, ReportPeriod.Season);
            AssertTrue(oldReport.HasPrevious || oldReport.TotalDays == 90, "pre-reset history present");

            world.Reset();
            session.Capture(world.State);
            nodes.Apply(session.Current);
            var report = RegionReportBuilder.Build(session.History, session.Current, RegionId.Empire, ReportPeriod.Year);
            var cmp = session.History.QueryCompare(HistoryMetric.Food, HistoryTimeRange.AllHistory);
            AssertTrue(session.History.Count(RegionId.Empire) == 1, "history cleared");
            AssertTrue(!report.HasPrevious, "report previous cleared");
            AssertTrue(report.TotalDays == 0, "report at day 0");
            AssertTrue(report.Line(HistoryMetric.Population).Current == world.Region(RegionId.Empire).Population, "report matches reset state");
            AssertTrue(cmp.For(RegionId.Empire).SampleCount == 1, "compare restarted");
            AssertTrue(cmp.For(RegionId.Empire).LastTotalDays == 0, "compare not stale");
            AssertTrue(nodes.Count == 15, "node count stable after reset");
            AssertTrue(nodes.Find(RegionId.Empire, ResourceNodeType.Food).Amount == session.Current.Find(RegionId.Empire).Food, "nodes match reset snapshot");
            AssertTrue(nodes.Find(RegionId.Empire, ResourceNodeType.Food).Amount != oldFood
                || Math.Abs(oldFood - session.Current.Find(RegionId.Empire).Food) < 0.01f,
                "nodes left previous run");
        }

        static void TestFastForward()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            var nodes = new ResourceNodeState();
            session.Capture(world.State);
            nodes.Apply(session.Current);
            float beforePop = session.Current.Find(RegionId.Sea).Population;
            int beforeCount = session.History.Count(RegionId.Sea);

            var ff = FastForwardSystem.FastForwardYears(world.State, world.Races, world.Config, 1);
            session.Capture(ff.State);
            nodes.Apply(session.Current);
            var report = RegionReportBuilder.Build(session.History, session.Current, RegionId.Sea, ReportPeriod.Year);
            var cmp = session.History.QueryCompare(HistoryMetric.Population, HistoryTimeRange.AllHistory);

            AssertTrue(session.History.Count(RegionId.Sea) == beforeCount + 1, "no duplicate FF samples");
            AssertTrue(session.History.Latest(RegionId.Sea).TotalDays == ff.State.TotalDays, "history at FF end");
            AssertTrue(report.TotalDays == ff.State.TotalDays, "report updated");
            AssertTrue(report.Line(HistoryMetric.Population).Current == session.Current.Find(RegionId.Sea).Population, "report == snapshot");
            AssertTrue(cmp.Current(RegionId.Sea) == session.Current.Find(RegionId.Sea).Population, "compare updated");
            AssertTrue(nodes.Find(RegionId.Sea, ResourceNodeType.Magic).Amount == session.Current.Find(RegionId.Sea).Magic, "magic node updated");
            AssertTrue(session.Current.Find(RegionId.Sea).Population != beforePop || ff.State.TotalDays > 0, "not stuck on pre-FF");
            AssertTrue(nodes.Count == 15, "FF must not spawn extra nodes");
        }

        static void TestNoNanInfinity()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            var nodes = new ResourceNodeState();
            RunDays(world, session, 360);
            nodes.Apply(session.Current);
            string reason;
            AssertTrue(!session.History.HasNonFiniteOrNegative(out reason), reason ?? "history");
            foreach (var id in AllRegions)
            {
                var report = RegionReportBuilder.Build(session.History, session.Current, id, ReportPeriod.Year);
                foreach (var line in report.Lines)
                {
                    AssertTrue(!float.IsNaN(line.Current) && !float.IsInfinity(line.Current), id + " " + line.Metric);
                    AssertTrue(!float.IsNaN(line.Percent) && !float.IsInfinity(line.Percent), id + " pct " + line.Metric);
                }
            }

            for (int i = 0; i < nodes.Nodes.Length; i++)
            {
                AssertTrue(!float.IsNaN(nodes.Nodes[i].Amount) && !float.IsInfinity(nodes.Nodes[i].Amount), "node amount");
            }
        }

        static void TestLongRun360()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            var nodes = new ResourceNodeState();
            RunDays(world, session, 360);
            nodes.Apply(session.Current);
            AssertTrue(nodes.Count == 15, "nodes " + nodes.Count);
            var cmp = session.History.QueryCompare(HistoryMetric.Stability, HistoryTimeRange.AllHistory);
            AssertTrue(cmp.For(RegionId.Theocracy).SampleCount == 361, "no duplicate history");
            int prev = -1;
            var samples = cmp.For(RegionId.Empire).Samples;
            for (int i = 0; i < samples.Length; i++)
            {
                AssertTrue(samples[i].TotalDays > prev, "dup " + samples[i].TotalDays);
                prev = samples[i].TotalDays;
            }

            string reason;
            AssertTrue(!session.History.HasNonFiniteOrNegative(out reason), reason ?? "360");
        }

        static void TestLongRun3600()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            var nodes = new ResourceNodeState();
            RunDays(world, session, 3600);
            nodes.Apply(session.Current);
            AssertTrue(nodes.Count == 15, "node explosion " + nodes.Count);
            AssertTrue(session.History.Count(RegionId.Sea) == 3601, "history " + session.History.Count(RegionId.Sea));
            var report = RegionReportBuilder.Build(session.History, session.Current, RegionId.Empire, ReportPeriod.Year);
            AssertTrue(report.HasPrevious, "3600 year compare");
            AssertTrue(!float.IsNaN(report.Line(HistoryMetric.Magic).Current), "magic finite");
            var cmp = session.History.QueryCompare(HistoryMetric.Education, HistoryTimeRange.Recent1Year);
            AssertTrue(cmp.RangeStart == cmp.For(RegionId.Theocracy).FirstTotalDays, "shared 1y");
            AssertTrue(cmp.RangeStart == cmp.For(RegionId.Empire).FirstTotalDays, "emp 1y");
            AssertTrue(cmp.RangeStart == cmp.For(RegionId.Sea).FirstTotalDays, "sea 1y");
            string reason;
            AssertTrue(!session.History.HasNonFiniteOrNegative(out reason), reason ?? "3600");
            Console.WriteLine("  3600d nodes=" + nodes.Count + " history/region=" + session.History.Count(RegionId.Theocracy));
        }

        static void TestP2AFreeze()
        {
            string root = FindRepoRoot();
            AssertTrue(root != null, "repo root");
            foreach (var file in FrozenFiles)
            {
                string path = Path.Combine(root, file.RelPath);
                AssertTrue(File.Exists(path), "missing " + file.RelPath);
                string hash = Sha256File(path);
                AssertTrue(hash == file.Sha256, file.RelPath + " freeze broken. expected " + file.Sha256 + " got " + hash);
            }

            var world = new HeadlessWorld();
            float[] pops = CopyPops(world);
            var session = new ObservationSession();
            session.Capture(world.State);
            RegionReportBuilder.Build(session.History, session.Current, RegionId.Empire, ReportPeriod.Year);
            session.History.QueryCompare(HistoryMetric.Food, HistoryTimeRange.AllHistory);
            var nodes = new ResourceNodeState();
            nodes.Apply(session.Current);
            float[] after = CopyPops(world);
            for (int i = 0; i < pops.Length; i++)
            {
                AssertTrue(pops[i] == after[i], "observation mutated population");
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

            string report = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Simulation/Observation/RegionReportBuilder.cs"));
            string nodesSrc = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Simulation/Observation/ResourceNodes.cs"));
            AssertTrue(!report.Contains("PopulationSystem"), "report must not call PopulationSystem");
            AssertTrue(!report.Contains("DailySimulation"), "report must not tick simulation");
            AssertTrue(!nodesSrc.Contains("PopulationSystem"), "resource nodes must not call PopulationSystem");
            string viz = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Simulation/Presentation/ResourceNodeVisualizer.cs"));
            AssertTrue(!viz.Contains("void Update"), "resource visualizer must not rebuild every Update");
        }

        static void TestProjectSettingsAndPackagesUnchanged()
        {
            string root = FindRepoRoot();
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
            AssertTrue(version.Contains("2022.3.62f3c1"), "Unity version");
            AssertTrue(version.Contains("1623fc0bbb97"), "Unity revision");
            string diff = GitDiffNames(root, "main", "ProjectSettings", "Packages");
            AssertTrue(string.IsNullOrWhiteSpace(diff), "ProjectSettings/Packages differ from main:\n" + diff);
        }

        static bool HasReportEvent(RegionReport report, SimEventType type)
        {
            for (int i = 0; i < report.MajorEvents.Length; i++)
            {
                if (report.MajorEvents[i].EventType == type)
                {
                    return true;
                }
            }

            return false;
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

        static void InjectEvent(HeadlessWorld world, RegionId regionId, SimEventType type, string eventId)
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
                StartDay = world.State.TotalDays,
                Duration = 8,
                Severity = 1f
            });
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
            var starts = new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory };
            foreach (var start in starts)
            {
                var dir = new DirectoryInfo(start);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, FrozenFiles[0].RelPath)))
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
