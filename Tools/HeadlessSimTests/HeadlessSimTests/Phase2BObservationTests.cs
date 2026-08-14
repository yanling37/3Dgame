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
    /// P2-B v0.3: observation snapshot → population visualizer rules.
    /// Does not modify P2-A simulation math.
    /// </summary>
    public static class Phase2BObservationTests
    {
        static readonly int[] Checkpoints = { 1, 30, 90, 180, 360 };

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
            Console.WriteLine("Divine World P2-B v0.3 Observation / Population Visualization (compat)");
            Console.WriteLine("============================================================");

            var failures = new List<string>();
            Run("HudTitle_Is_v0.4", TestHudTitle, failures);
            Run("Snapshot_CopiesPopulationExactly", TestSnapshotCopiesPopulation, failures);
            Run("Snapshot_IsIndependentCopy", TestSnapshotIsCopy, failures);
            Run("Visualizer_MatchesSnapshot", TestVisualizerMatchesSnapshot, failures);
            Run("Visualizer_UsesSnapshotNotLiveState", TestVisualizerUsesSnapshotNotLiveState, failures);
            Run("Visualizer_Source_ReadsSnapshotPopulation", TestVisualizerSourceReadsSnapshot, failures);
            Run("Regions_AreIndependent", TestRegionsIndependent, failures);
            Run("Checkpoints_Day1_30_90_180_360", TestCheckpoints, failures);
            Run("PopulationDeclineAndGrowth", TestDeclineAndGrowth, failures);
            Run("PopulationNearZero", TestNearZero, failures);
            Run("NoNegativeMarkers", TestNoNegativeMarkers, failures);
            Run("NoNaNOrInfinity", TestNoNanInfinity, failures);
            Run("MaxMarkersPerRegion", TestMaxMarkers, failures);
            Run("MaxMarkers_DoesNotAffectOtherRegions", TestMaxMarkersIndependent, failures);
            Run("Threshold_OnlyUpdatesCountOnCrossing", TestThresholdUpdates, failures);
            Run("NoPerPersonObjects", TestBoundedAllocation, failures);
            Run("Reset_RebuildsInitialVisuals", TestReset, failures);
            Run("Reset_ClearsStaleMarkerCounts", TestResetClearsStaleMarkers, failures);
            Run("FastForward_UsesNewSnapshot", TestFastForward, failures);
            Run("FastForward_VisualNotStale", TestFastForwardVisualNotStale, failures);
            Run("P2A_Freeze_HashesAndNoMutation", TestP2AFreeze, failures);
            Run("ProjectSettings_And_Packages_Unchanged", TestProjectSettingsAndPackagesUnchanged, failures);

            Console.WriteLine();
            const int total = 22;
            Console.WriteLine($"Result: {total - failures.Count}/{total} passed");
            foreach (var f in failures)
            {
                Console.WriteLine("FAIL: " + f);
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

        static void TestSnapshotCopiesPopulation()
        {
            var world = new HeadlessWorld();
            var snap = ObservationCapture.FromWorld(world.State);
            foreach (var region in world.State.Regions)
            {
                var observed = snap.Find(region.Id);
                AssertTrue(observed != null, "missing " + region.Id);
                AssertTrue(observed.Population == region.Population,
                    region.Id + " snapshot " + observed.Population + " != state " + region.Population);
            }
        }

        static void TestSnapshotIsCopy()
        {
            var world = new HeadlessWorld();
            float original = world.Region(RegionId.Empire).Population;
            var snap = ObservationCapture.FromWorld(world.State);
            world.Region(RegionId.Empire).Population = 1f;
            AssertTrue(snap.Find(RegionId.Empire).Population == original,
                "snapshot must not alias live RegionState.Population");
        }

        static void TestVisualizerMatchesSnapshot()
        {
            var world = new HeadlessWorld();
            var cfg = PopulationVisualizationConfig.CreateDefault();
            var session = new ObservationSession();
            var visual = new PopulationVisualState();
            session.Capture(world.State);
            visual.Apply(session.Current, cfg);

            foreach (var region in session.Current.Regions)
            {
                int expected = PopulationMarkerRules.MarkerCount(region.Population, cfg);
                int actual = visual.VisibleCount(region.RegionId);
                AssertTrue(actual == expected,
                    region.RegionId + " markers " + actual + " != rules " + expected + " for pop " + region.Population);
                AssertTrue(actual >= 0, "negative markers");
            }
        }

        static void TestVisualizerUsesSnapshotNotLiveState()
        {
            var world = new HeadlessWorld();
            var cfg = PopulationVisualizationConfig.CreateDefault();
            var snap = ObservationCapture.FromWorld(world.State);
            var visual = new PopulationVisualState();
            visual.Apply(snap, cfg);

            float snapPop = snap.Find(RegionId.Empire).Population;
            int fromSnapshot = visual.VisibleCount(RegionId.Empire);
            AssertTrue(fromSnapshot == PopulationMarkerRules.MarkerCount(snapPop, cfg), "baseline from snapshot.Population");
            AssertTrue(fromSnapshot > 0, "day-0 empire should have markers");

            world.Region(RegionId.Empire).Population = 0f;
            world.Region(RegionId.Theocracy).Population = cfg.PopulationPerMarker * 50f;
            visual.Apply(snap, cfg);

            AssertTrue(visual.VisibleCount(RegionId.Empire) == fromSnapshot,
                "visualizer must keep snapshot.Population, not live WorldState (empire)");
            AssertTrue(
                visual.VisibleCount(RegionId.Theocracy)
                == PopulationMarkerRules.MarkerCount(snap.Find(RegionId.Theocracy).Population, cfg),
                "visualizer must keep snapshot.Population, not live WorldState (theocracy)");
            AssertTrue(fromSnapshot != 0, "stale live-state zero must not win over snapshot");
        }

        static void TestVisualizerSourceReadsSnapshot()
        {
            string root = FindRepoRoot();
            AssertTrue(root != null, "repo root");
            string vizPath = Path.Combine(root, "Assets/Scripts/Simulation/Presentation/PopulationVisualizer.cs");
            string capturePath = Path.Combine(root, "Assets/Scripts/Simulation/Observation/ObservationCapture.cs");
            AssertTrue(File.Exists(vizPath), "PopulationVisualizer.cs missing");
            AssertTrue(File.Exists(capturePath), "ObservationCapture.cs missing");

            string viz = File.ReadAllText(vizPath);
            string capture = File.ReadAllText(capturePath);
            AssertTrue(viz.Contains("WorldObservationSnapshot"), "PopulationVisualizer must consume observation snapshots");
            AssertTrue(viz.Contains("region.Population"), "PopulationVisualizer must read snapshot.Population");
            AssertTrue(!viz.Contains("SimulationWorld"), "PopulationVisualizer must not read SimulationWorld");
            AssertTrue(!viz.Contains("world.State"), "PopulationVisualizer must not read world.State");
            AssertTrue(!capture.Contains("PopulationSystem"), "ObservationCapture must not call PopulationSystem");
            AssertTrue(capture.Contains("region.Population"), "ObservationCapture must copy RegionState.Population");
        }

        static void TestRegionsIndependent()
        {
            var world = new HeadlessWorld();
            var cfg = PopulationVisualizationConfig.CreateDefault();
            var visual = new PopulationVisualState();
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);

            int empireBefore = visual.VisibleCount(RegionId.Empire);
            int seaBefore = visual.VisibleCount(RegionId.Sea);
            AssertTrue(empireBefore > 0 && seaBefore > 0, "baseline markers should be visible");

            world.Region(RegionId.Theocracy).Population = 0f;
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);

            AssertTrue(visual.VisibleCount(RegionId.Theocracy) == 0, "theocracy should drop to 0");
            AssertTrue(visual.VisibleCount(RegionId.Empire) == empireBefore, "empire markers must not change");
            AssertTrue(visual.VisibleCount(RegionId.Sea) == seaBefore, "sea markers must not change");

            world.Region(RegionId.Empire).Population = world.Region(RegionId.Empire).Population * 2f;
            int seaStill = visual.VisibleCount(RegionId.Sea);
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            AssertTrue(visual.VisibleCount(RegionId.Sea) == seaStill, "sea unchanged after empire growth");
            AssertTrue(visual.VisibleCount(RegionId.Theocracy) == 0, "theocracy stays at 0");
        }

        static void TestCheckpoints()
        {
            var world = new HeadlessWorld();
            var cfg = PopulationVisualizationConfig.CreateDefault();
            var visual = new PopulationVisualState();
            int next = 0;
            for (int day = 1; day <= 360; day++)
            {
                world.AdvanceDay();
                if (next < Checkpoints.Length && day == Checkpoints[next])
                {
                    var snap = ObservationCapture.FromWorld(world.State);
                    visual.Apply(snap, cfg);
                    AssertSnapshotMatchesState(world, snap);
                    AssertVisualMatchesSnapshot(visual, snap, cfg);
                    Console.WriteLine(
                        "  day " + day
                        + " Theo=" + snap.Find(RegionId.Theocracy).Population.ToString("0")
                        + " Emp=" + snap.Find(RegionId.Empire).Population.ToString("0")
                        + " Sea=" + snap.Find(RegionId.Sea).Population.ToString("0")
                        + " markers "
                        + visual.VisibleCount(RegionId.Theocracy) + "/"
                        + visual.VisibleCount(RegionId.Empire) + "/"
                        + visual.VisibleCount(RegionId.Sea));
                    next++;
                }
            }
        }

        static void TestDeclineAndGrowth()
        {
            var cfg = PopulationVisualizationConfig.CreateDefault();
            var visual = new PopulationVisualState();
            var world = new HeadlessWorld();

            world.Region(RegionId.Empire).Population = cfg.PopulationPerMarker * 8f;
            var mid = ObservationCapture.FromWorld(world.State);
            visual.Apply(mid, cfg);
            int midCount = visual.VisibleCount(RegionId.Empire);

            world.Region(RegionId.Empire).Population = cfg.PopulationPerMarker * 3f;
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            int down = visual.VisibleCount(RegionId.Empire);
            AssertTrue(down < midCount, "decline should reduce markers: " + down + " vs " + midCount);

            world.Region(RegionId.Empire).Population = cfg.PopulationPerMarker * 12f;
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            int up = visual.VisibleCount(RegionId.Empire);
            AssertTrue(up > down, "growth should increase markers: " + up + " vs " + down);
        }

        static void TestNearZero()
        {
            var cfg = PopulationVisualizationConfig.CreateDefault();
            var visual = new PopulationVisualState();
            var world = new HeadlessWorld();

            world.Region(RegionId.Sea).Population = 0f;
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            AssertTrue(visual.VisibleCount(RegionId.Sea) == 0, "pop 0 → 0 markers");

            world.Region(RegionId.Sea).Population = cfg.PopulationPerMarker * 0.5f;
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            AssertTrue(visual.VisibleCount(RegionId.Sea) == 0, "below threshold → 0 markers");

            world.Region(RegionId.Sea).Population = cfg.PopulationPerMarker;
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            AssertTrue(visual.VisibleCount(RegionId.Sea) == 1, "at threshold → 1 marker");
        }

        static void TestNoNegativeMarkers()
        {
            var cfg = PopulationVisualizationConfig.CreateDefault();
            var visual = new PopulationVisualState();
            var world = new HeadlessWorld();
            world.Region(RegionId.Theocracy).Population = -5000f;
            world.Region(RegionId.Empire).Population = -1f;
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);

            AssertTrue(!visual.HasNegativeVisible(), "no negative marker counts");
            AssertTrue(visual.VisibleCount(RegionId.Theocracy) == 0, "negative pop → 0");
            AssertTrue(PopulationMarkerRules.MarkerCount(-10f, cfg) == 0, "rules reject negative pop");
        }

        static void TestNoNanInfinity()
        {
            var cfg = PopulationVisualizationConfig.CreateDefault();
            var visual = new PopulationVisualState();
            var world = new HeadlessWorld();
            float[] bad = { float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            foreach (var value in bad)
            {
                world.Region(RegionId.Empire).Population = value;
                var snap = ObservationCapture.FromWorld(world.State);
                visual.Apply(snap, cfg);
                var plan = PopulationMarkerRules.Evaluate(value, cfg);
                AssertTrue(plan.MarkerCount >= 0, "count");
                AssertTrue(!float.IsNaN(plan.MarkerScale) && !float.IsInfinity(plan.MarkerScale), "scale finite for " + value);
                AssertTrue(!visual.HasNonFiniteScale(), "visual scale finite");
                AssertTrue(visual.VisibleCount(RegionId.Empire) == 0, "bad pop → 0 markers");
            }

            var brokenCfg = new PopulationVisualizationConfig
            {
                PopulationPerMarker = float.NaN,
                MaxMarkersPerRegion = 8,
                MinMarkerScale = float.PositiveInfinity,
                MaxMarkerScale = float.NaN
            };
            var plan2 = PopulationMarkerRules.Evaluate(4000f, brokenCfg);
            AssertTrue(plan2.MarkerCount >= 0 && plan2.MarkerCount <= PopulationVisualizationConfig.DefaultMaxMarkersPerRegion, "sanitized count");
            AssertTrue(!float.IsNaN(plan2.MarkerScale) && !float.IsInfinity(plan2.MarkerScale), "sanitized scale");
        }

        static void TestMaxMarkers()
        {
            var cfg = PopulationVisualizationConfig.CreateDefault();
            cfg.MaxMarkersPerRegion = 5;
            var visual = new PopulationVisualState();
            var world = new HeadlessWorld();
            world.Region(RegionId.Empire).Population = cfg.PopulationPerMarker * 1000f;
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            AssertTrue(visual.VisibleCount(RegionId.Empire) == 5, "capped at max, got " + visual.VisibleCount(RegionId.Empire));
            AssertTrue(visual.AllocatedCount(RegionId.Empire) == 5, "allocated == max");
            AssertTrue(visual.AllocatedMarkerSlots <= 5 * world.State.Regions.Length, "no unbounded allocation");
        }

        static void TestMaxMarkersIndependent()
        {
            var cfg = PopulationVisualizationConfig.CreateDefault();
            cfg.MaxMarkersPerRegion = 4;
            var visual = new PopulationVisualState();
            var world = new HeadlessWorld();
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            int seaBefore = visual.VisibleCount(RegionId.Sea);
            int theoBefore = visual.VisibleCount(RegionId.Theocracy);

            world.Region(RegionId.Empire).Population = cfg.PopulationPerMarker * 1000f;
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            AssertTrue(visual.VisibleCount(RegionId.Empire) == 4, "empire capped");
            AssertTrue(visual.VisibleCount(RegionId.Sea) == seaBefore, "sea cap independent");
            AssertTrue(visual.VisibleCount(RegionId.Theocracy) == theoBefore, "theocracy cap independent");
            AssertTrue(visual.AllocatedCount(RegionId.Empire) <= 4, "empire allocated cap");
            AssertTrue(visual.AllocatedCount(RegionId.Sea) <= 4, "sea allocated cap");
        }

        static void TestThresholdUpdates()
        {
            var cfg = new PopulationVisualizationConfig
            {
                PopulationPerMarker = 1000f,
                MaxMarkersPerRegion = 10,
                MinMarkerScale = 0.1f,
                MaxMarkerScale = 0.4f
            };
            var visual = new PopulationVisualState();
            var world = new HeadlessWorld();
            world.Region(RegionId.Theocracy).Population = 1999f;
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            AssertTrue(visual.VisibleCount(RegionId.Theocracy) == 1, "1999 → 1");
            int changes = visual.CountChangeEvents;

            world.Region(RegionId.Theocracy).Population = 1999.9f;
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            AssertTrue(visual.VisibleCount(RegionId.Theocracy) == 1, "still 1 below 2000");
            AssertTrue(visual.CountChangeEvents == changes, "count must not update without crossing threshold");

            world.Region(RegionId.Theocracy).Population = 2000f;
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            AssertTrue(visual.VisibleCount(RegionId.Theocracy) == 2, "2000 → 2");
            AssertTrue(visual.CountChangeEvents == changes + 1, "count updates once on threshold");
        }

        static void TestBoundedAllocation()
        {
            var cfg = PopulationVisualizationConfig.CreateDefault();
            var visual = new PopulationVisualState();
            var world = new HeadlessWorld();
            int maxSlots = cfg.MaxMarkersPerRegion * world.State.Regions.Length;
            for (int i = 0; i < 360; i++)
            {
                world.AdvanceDay();
                visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            }

            AssertTrue(visual.AllocatedMarkerSlots <= maxSlots,
                "allocated " + visual.AllocatedMarkerSlots + " > cap " + maxSlots);
            AssertTrue(visual.DestroyEvents == 0, "population ticks must not destroy pooled slots");
            AssertTrue(visual.PoolGrowEvents <= world.State.Regions.Length,
                "pool should grow once per region, grew " + visual.PoolGrowEvents);
        }

        static void TestReset()
        {
            var cfg = PopulationVisualizationConfig.CreateDefault();
            var visual = new PopulationVisualState();
            var world = new HeadlessWorld();
            var day0 = ObservationCapture.FromWorld(world.State);
            visual.Apply(day0, cfg);
            int[] day0Counts =
            {
                visual.VisibleCount(RegionId.Theocracy),
                visual.VisibleCount(RegionId.Empire),
                visual.VisibleCount(RegionId.Sea)
            };
            float[] day0Pops =
            {
                day0.Find(RegionId.Theocracy).Population,
                day0.Find(RegionId.Empire).Population,
                day0.Find(RegionId.Sea).Population
            };

            world.AdvanceDays(90);
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);

            world.Reset();
            var after = ObservationCapture.FromWorld(world.State);
            visual.RebuildFrom(after, cfg);

            AssertTrue(after.Find(RegionId.Theocracy).Population == day0Pops[0], "theocracy pop restored");
            AssertTrue(after.Find(RegionId.Empire).Population == day0Pops[1], "empire pop restored");
            AssertTrue(after.Find(RegionId.Sea).Population == day0Pops[2], "sea pop restored");
            AssertTrue(visual.VisibleCount(RegionId.Theocracy) == day0Counts[0], "theocracy markers restored");
            AssertTrue(visual.VisibleCount(RegionId.Empire) == day0Counts[1], "empire markers restored");
            AssertTrue(visual.VisibleCount(RegionId.Sea) == day0Counts[2], "sea markers restored");
            AssertTrue(visual.DestroyEvents == 0, "reset must reuse pools, not destroy");
        }

        static void TestResetClearsStaleMarkers()
        {
            var cfg = new PopulationVisualizationConfig
            {
                PopulationPerMarker = 1000f,
                MaxMarkersPerRegion = 80,
                MinMarkerScale = PopulationVisualizationConfig.DefaultMinMarkerScale,
                MaxMarkerScale = PopulationVisualizationConfig.DefaultMaxMarkerScale
            };
            var visual = new PopulationVisualState();
            var world = new HeadlessWorld();
            var day0 = ObservationCapture.FromWorld(world.State);
            visual.Apply(day0, cfg);
            int seaDay0 = visual.VisibleCount(RegionId.Sea);
            int empDay0 = visual.VisibleCount(RegionId.Empire);
            AssertTrue(seaDay0 > 0 && empDay0 > 0, "day-0 markers present");

            world.Region(RegionId.Sea).Population = 0f;
            world.Region(RegionId.Empire).Population = cfg.PopulationPerMarker * 70f;
            visual.Apply(ObservationCapture.FromWorld(world.State), cfg);
            AssertTrue(visual.VisibleCount(RegionId.Sea) == 0, "sea cleared to 0 before reset");
            AssertTrue(visual.VisibleCount(RegionId.Empire) == 70, "empire inflated before reset");

            world.Reset();
            var after = ObservationCapture.FromWorld(world.State);
            visual.RebuildFrom(after, cfg);
            AssertTrue(visual.VisibleCount(RegionId.Sea) == seaDay0, "reset must restore sea, not keep 0 leftover");
            AssertTrue(visual.VisibleCount(RegionId.Empire) == empDay0, "reset must drop inflated empire markers");
            AssertTrue(visual.VisibleCount(RegionId.Empire) != 70, "old inflated visual must not remain");
            AssertVisualMatchesSnapshot(visual, after, cfg);
            AssertTrue(visual.DestroyEvents == 0, "reset reuses pool");
        }

        static void TestFastForward()
        {
            var cfg = PopulationVisualizationConfig.CreateDefault();
            var visual = new PopulationVisualState();
            var world = new HeadlessWorld();
            var before = ObservationCapture.FromWorld(world.State);
            visual.Apply(before, cfg);
            float[] beforePops =
            {
                before.Find(RegionId.Theocracy).Population,
                before.Find(RegionId.Empire).Population,
                before.Find(RegionId.Sea).Population
            };
            int[] beforeCounts =
            {
                visual.VisibleCount(RegionId.Theocracy),
                visual.VisibleCount(RegionId.Empire),
                visual.VisibleCount(RegionId.Sea)
            };

            var ff = FastForwardSystem.FastForwardYears(world.State, world.Races, world.Config, 1);
            AssertTrue(ff.State != null, "fast-forward produced state");
            var after = ObservationCapture.FromWorld(ff.State);
            visual.Apply(after, cfg);

            AssertSnapshotMatchesState(ff.State, after);
            AssertVisualMatchesSnapshot(visual, after, cfg);

            bool anyPopChanged = false;
            var ids = new[] { RegionId.Theocracy, RegionId.Empire, RegionId.Sea };
            for (int i = 0; i < ids.Length; i++)
            {
                float live = after.Find(ids[i]).Population;
                AssertTrue(live != beforePops[i] || visual.VisibleCount(ids[i]) == beforeCounts[i],
                    "snapshot should follow FF state");
                if (Math.Abs(live - beforePops[i]) > 0.5f)
                {
                    anyPopChanged = true;
                }

                var liveRegion = RegionLookup.FindRegion(ff.State.Regions, ids[i]);
                AssertTrue(liveRegion != null, "missing FF region " + ids[i]);
                AssertTrue(after.Find(ids[i]).Population == liveRegion.Population,
                    "FF snapshot must be the new state, not the pre-FF population");
            }

            AssertTrue(anyPopChanged, "1-year fast-forward should change at least one region's population");
            AssertTrue(after.TotalDays != before.TotalDays, "FF snapshot day must advance");
        }

        static void TestFastForwardVisualNotStale()
        {
            var cfg = PopulationVisualizationConfig.CreateDefault();
            var visual = new PopulationVisualState();
            var world = new HeadlessWorld();
            var before = ObservationCapture.FromWorld(world.State);
            visual.Apply(before, cfg);
            int[] beforeCounts =
            {
                visual.VisibleCount(RegionId.Theocracy),
                visual.VisibleCount(RegionId.Empire),
                visual.VisibleCount(RegionId.Sea)
            };

            var ff = FastForwardSystem.FastForwardYears(world.State, world.Races, world.Config, 1);
            var after = ObservationCapture.FromWorld(ff.State);
            visual.Apply(after, cfg);

            AssertSnapshotMatchesState(ff.State, after);
            AssertVisualMatchesSnapshot(visual, after, cfg);

            bool visualMoved = false;
            var ids = new[] { RegionId.Theocracy, RegionId.Empire, RegionId.Sea };
            for (int i = 0; i < ids.Length; i++)
            {
                int expected = PopulationMarkerRules.MarkerCount(after.Find(ids[i]).Population, cfg);
                int actual = visual.VisibleCount(ids[i]);
                AssertTrue(actual == expected, ids[i] + " FF visual " + actual + " != " + expected);
                int stale = PopulationMarkerRules.MarkerCount(before.Find(ids[i]).Population, cfg);
                AssertTrue(actual != stale || expected == stale, ids[i] + " must not keep pre-FF marker count when snapshot changed");
                if (actual != beforeCounts[i])
                {
                    visualMoved = true;
                }
            }

            AssertTrue(visualMoved, "FastForward 1y should change at least one region's visible marker count");
            AssertTrue(after.TotalDays >= 360, "FF snapshot is the post-year state, not day 0");
        }

        static void TestP2AFreeze()
        {
            string root = FindRepoRoot();
            AssertTrue(root != null, "could not locate repo root for freeze hashes");
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
            var snap = ObservationCapture.FromWorld(world.State);
            var visual = new PopulationVisualState();
            visual.Apply(snap, PopulationVisualizationConfig.CreateDefault());
            float[] after = CopyPops(world);
            for (int i = 0; i < pops.Length; i++)
            {
                AssertTrue(pops[i] == after[i], "observation/visualizer mutated simulation population");
            }

            world.AdvanceDay();
            AssertTrue(!world.State.HaltedOnNumericError, world.State.LastNumericError ?? "halt");
            foreach (var r in world.State.Regions)
            {
                AssertTrue(NumericGuard.IsFinite(r.Population) && r.Population >= 0f, r.Id + " pop");
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

        static void AssertSnapshotMatchesState(HeadlessWorld world, WorldObservationSnapshot snap)
        {
            AssertSnapshotMatchesState(world.State, snap);
        }

        static void AssertSnapshotMatchesState(WorldState state, WorldObservationSnapshot snap)
        {
            foreach (var region in state.Regions)
            {
                var observed = snap.Find(region.Id);
                AssertTrue(observed != null, "missing " + region.Id);
                AssertTrue(observed.Population == region.Population,
                    region.Id + " " + observed.Population + " != " + region.Population);
            }
        }

        static void AssertVisualMatchesSnapshot(
            PopulationVisualState visual,
            WorldObservationSnapshot snap,
            PopulationVisualizationConfig cfg)
        {
            foreach (var region in snap.Regions)
            {
                int expected = PopulationMarkerRules.MarkerCount(region.Population, cfg);
                AssertTrue(visual.VisibleCount(region.RegionId) == expected,
                    region.RegionId + " visual " + visual.VisibleCount(region.RegionId) + " != " + expected);
            }
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
