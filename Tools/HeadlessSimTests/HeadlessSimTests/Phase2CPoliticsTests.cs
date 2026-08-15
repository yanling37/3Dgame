using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;
using DivineWorld.Simulation.Politics;
using DivineWorld.Simulation.Systems;

namespace HeadlessSimTests
{
    /// <summary>
    /// P2-C v0.1: undirected political relations. Does not modify P2-A math or P2-B history buffers.
    /// </summary>
    public static class Phase2CPoliticsTests
    {
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

        static readonly string[] FrozenRelPaths =
        {
            "Assets/Scripts/Simulation/Systems/PopulationSystem.cs",
            "Assets/Scripts/Simulation/Systems/ResourceSystem.cs",
            "Assets/Scripts/Simulation/Systems/SeasonSystem.cs",
            "Assets/Scripts/Simulation/Systems/WeatherSystem.cs",
            "Assets/Scripts/Simulation/Systems/EventSystem.cs",
            "Assets/Scripts/Simulation/Systems/FastForwardSystem.cs",
            "Assets/Scripts/Simulation/Systems/SocietySystem.cs",
            "Assets/Scripts/Simulation/Data/SimulationConfig.cs",
            "Assets/Scripts/Simulation/Core/DailySimulation.cs"
        };

        static readonly string[] WarBannedTokens =
        {
            "Army", "Military", "Battle", "Occupation", "Casualties", "Frontline", "Siege", "WarSupply"
        };

        public static int Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("Divine World P2-C v0.1 Politics / Diplomatic Relations");
            Console.WriteLine("=====================================================");

            var failures = new List<string>();
            Run("HudTitle_Is_P2C_Politics_v0.1", TestHudTitle, failures);
            Run("InitialRelations", TestInitialRelations, failures);
            Run("InitialRelations_FromConfig_NotPopulation", TestInitialFromConfig, failures);
            Run("RelationValueRange", TestValueRange, failures);
            Run("RelationStateThreshold", TestStateThreshold, failures);
            Run("War_IsReservedAndNeverAssigned", TestWarReserved, failures);
            Run("PairIsolation", TestPairIsolation, failures);
            Run("RelationSymmetry", TestSymmetry, failures);
            Run("RelationHistory", TestHistory, failures);
            Run("DebugAdjustment", TestDebugAdjustment, failures);
            Run("Reset", TestReset, failures);
            Run("FastForward_DoesNotDrift", TestFastForward, failures);
            Run("P2B_Compatibility", TestP2BCompatibility, failures);
            Run("NoPopulationResourceSideEffects", TestNoSideEffects, failures);
            Run("NaN_Infinity", TestNanInfinity, failures);
            Run("LongRun_360", TestLongRun360, failures);
            Run("LongRun_3600", TestLongRun3600, failures);
            Run("P2A_Freeze", TestP2AFreeze, failures);
            Run("ProjectSettings_And_Packages_Unchanged", TestProjectSettingsAndPackagesUnchanged, failures);
            Run("NoWarLogic", TestNoWarLogic, failures);

            Console.WriteLine();
            const int total = 20;
            Console.WriteLine($"Result: {total - failures.Count}/{total} passed");
            foreach (var f in failures)
            {
                Console.WriteLine("FAIL: " + f);
            }

            Console.WriteLine();
            Console.WriteLine(failures.Count == 0
                ? "P2-C v0.1 AUTOMATED TEST = PASS"
                : "P2-C v0.1 AUTOMATED TEST = FAIL");

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
            AssertTrue(PoliticsVersion.HudTitle == "P2-C · Politics v0.1", PoliticsVersion.HudTitle);
            AssertTrue(PoliticsVersion.Number == "v0.1", PoliticsVersion.Number);
            AssertTrue(ObservationVersion.HudTitle == "P2-B · Observation v0.5", ObservationVersion.HudTitle);
        }

        static void TestInitialRelations()
        {
            var world = new HeadlessWorld();
            var politics = world.State.Politics;
            AssertTrue(politics != null, "Politics missing on new world");
            AssertTrue(politics.Relations != null && politics.Relations.Count == 3, "expected 3 pairs, got " + (politics.Relations?.Count ?? -1));

            var cfg = PoliticalConfig.CreateDefault();
            AssertPair(world, RegionId.Theocracy, RegionId.Empire, LookupInitial(cfg, RegionId.Theocracy, RegionId.Empire));
            AssertPair(world, RegionId.Theocracy, RegionId.Sea, LookupInitial(cfg, RegionId.Theocracy, RegionId.Sea));
            AssertPair(world, RegionId.Empire, RegionId.Sea, LookupInitial(cfg, RegionId.Empire, RegionId.Sea));

            foreach (var relation in politics.Relations)
            {
                AssertTrue(relation.History != null && relation.History.Count == 0, relation.PairLabel + " history must start empty");
                AssertTrue(relation.LastChangedDay == 0, relation.PairLabel + " LastChangedDay");
                AssertTrue((int)relation.SourceRegionId < (int)relation.TargetRegionId, "canonical order " + relation.PairLabel);
            }
        }

        static void TestInitialFromConfig()
        {
            var cfg = PoliticalConfig.CreateDefault();
            cfg.InitialPoliticalRelations[0].RelationValue = 12f;
            cfg.InitialPoliticalRelations[1].RelationValue = -8f;
            cfg.InitialPoliticalRelations[2].RelationValue = 40f;

            var politics = PoliticsSystem.CreateInitialState(cfg);
            AssertTrue(politics.FindRelation(RegionId.Theocracy, RegionId.Empire).RelationValue == 12f, "TE from config");
            AssertTrue(politics.FindRelation(RegionId.Theocracy, RegionId.Sea).RelationValue == -8f, "TS from config");
            AssertTrue(politics.FindRelation(RegionId.Empire, RegionId.Sea).RelationValue == 40f, "ES from config");

            var world = new HeadlessWorld();
            world.Region(RegionId.Theocracy).Population = 1f;
            world.Region(RegionId.Empire).Set(ResourceId.Food, 1f);
            var after = PoliticsSystem.CreateInitialState();
            AssertTrue(after.FindRelation(RegionId.Theocracy, RegionId.Empire).RelationValue
                == PoliticalConfig.CreateDefault().InitialPoliticalRelations[0].RelationValue,
                "initial relations must not be derived from population/resources");
        }

        static void TestValueRange()
        {
            var world = new HeadlessWorld();
            var cfg = world.State.Politics.Config;
            for (int i = 0; i < 20; i++)
            {
                PoliticsSystem.DebugAdjust(world.State, RegionId.Theocracy, RegionId.Empire, cfg.DebugAdjustmentMagnitude);
            }

            var te = world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Empire);
            AssertTrue(te.RelationValue == cfg.MaxRelationValue, "upper clamp " + te.RelationValue);
            AssertTrue(te.RelationValue <= 100f && te.RelationValue >= -100f, "range");

            world.Reset();
            for (int i = 0; i < 20; i++)
            {
                PoliticsSystem.DebugAdjust(world.State, RegionId.Theocracy, RegionId.Empire, -cfg.DebugAdjustmentMagnitude);
            }

            te = world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Empire);
            AssertTrue(te.RelationValue == cfg.MinRelationValue, "lower clamp " + te.RelationValue);
        }

        static void TestStateThreshold()
        {
            var cfg = PoliticalConfig.CreateDefault();
            AssertTrue(PoliticsSystem.ResolveState(75f, cfg) == PoliticalRelationState.Friendly, "+75 friendly");
            AssertTrue(PoliticsSystem.ResolveState(cfg.FriendlyMin, cfg) == PoliticalRelationState.Friendly, "FriendlyMin inclusive");
            AssertTrue(PoliticsSystem.ResolveState(cfg.FriendlyMin - 0.01f, cfg) == PoliticalRelationState.Neutral, "below FriendlyMin");
            AssertTrue(PoliticsSystem.ResolveState(25f, cfg) == PoliticalRelationState.Neutral, "+25 normal/neutral");
            AssertTrue(PoliticsSystem.ResolveState(0f, cfg) == PoliticalRelationState.Neutral, "0 neutral");
            AssertTrue(PoliticsSystem.ResolveState(cfg.TenseMax, cfg) == PoliticalRelationState.Tense, "TenseMax inclusive");
            AssertTrue(PoliticsSystem.ResolveState(-25f, cfg) == PoliticalRelationState.Tense, "-25 tense");
            AssertTrue(PoliticsSystem.ResolveState(cfg.HostileMax, cfg) == PoliticalRelationState.Hostile, "HostileMax inclusive");
            AssertTrue(PoliticsSystem.ResolveState(-75f, cfg) == PoliticalRelationState.Hostile, "-75 hostile");
            AssertTrue(PoliticsSystem.ResolveState(-100f, cfg) == PoliticalRelationState.Hostile, "-100 hostile");

            var world = new HeadlessWorld();
            PushTo(world, RegionId.Theocracy, RegionId.Sea, 60f);
            AssertTrue(world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Sea).RelationState
                == PoliticalRelationState.Friendly, "stored state tracks value");
        }

        static void TestWarReserved()
        {
            AssertTrue(!WarReservation.Implemented, "war not implemented");
            AssertTrue(WarReservation.Status == "Reserved / NotImplemented", WarReservation.Status);
            var cfg = PoliticalConfig.CreateDefault();
            for (float v = -100f; v <= 100f; v += 1f)
            {
                AssertTrue(PoliticsSystem.ResolveState(v, cfg) != PoliticalRelationState.War,
                    "War assigned at " + v);
            }
        }

        static void TestPairIsolation()
        {
            var world = new HeadlessWorld();
            var before = SnapshotWorld(world);
            float ts = Value(world, RegionId.Theocracy, RegionId.Sea);
            float es = Value(world, RegionId.Empire, RegionId.Sea);

            AssertTrue(PoliticsSystem.DebugAdjust(world.State, RegionId.Theocracy, RegionId.Empire, 10f), "adjust TE");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == 10f, "TE changed");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Sea) == ts, "TS must not change");
            AssertTrue(Value(world, RegionId.Empire, RegionId.Sea) == es, "ES must not change");
            AssertUnchanged(before, SnapshotWorld(world), "pair isolation must not touch region stats");
        }

        static void TestSymmetry()
        {
            var world = new HeadlessWorld();
            var ab = world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Empire);
            var ba = world.State.Politics.FindRelation(RegionId.Empire, RegionId.Theocracy);
            AssertTrue(ReferenceEquals(ab, ba), "undirected pair must be one object");
            AssertTrue(world.State.Politics.Relations.Count == 3, "no directed duplicates");

            PoliticsSystem.DebugAdjust(world.State, RegionId.Empire, RegionId.Theocracy, -10f);
            AssertTrue(ab.RelationValue == -10f, "adjust reverse direction writes the same pair");
            AssertTrue(ba.RelationValue == -10f, "symmetric read");
            AssertTrue(world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Theocracy) == null, "no self pair");
        }

        static void TestHistory()
        {
            var world = new HeadlessWorld();
            world.AdvanceDays(120);
            var session = new ObservationSession();
            session.Capture(world.State);
            int regionHistory = session.History.TotalEntryCount();

            PushTo(world, RegionId.Empire, RegionId.Sea, 20f);
            AssertTrue(PoliticsSystem.AdjustRelation(
                world.State,
                RegionId.Empire,
                RegionId.Sea,
                -30f,
                "Diplomatic Incident"), "incident");

            var history = world.State.Politics.GetHistory(RegionId.Sea, RegionId.Empire);
            AssertTrue(history.Count >= 1, "history belongs to the pair");
            var last = history[history.Count - 1];
            AssertTrue(last.Day == 120, "history day " + last.Day);
            AssertTrue(last.OldValue == 20f, "old " + last.OldValue);
            AssertTrue(last.NewValue == -10f, "new " + last.NewValue);
            AssertTrue(last.Reason == "Diplomatic Incident", last.Reason);
            AssertTrue(last.SourceRegionId == RegionId.Empire && last.TargetRegionId == RegionId.Sea, "canonical pair on entry");

            string line = last.ToObservationLine();
            AssertTrue(line.Contains("Day 120"), line);
            AssertTrue(line.Contains("Empire ↔ Sea"), line);
            AssertTrue(line.Contains("Diplomatic Incident"), line);

            AssertTrue(session.History.TotalEntryCount() == regionHistory,
                "political history must not write ObservationHistory without recapture");
            session.Capture(world.State);
            AssertTrue(session.History.TotalEntryCount() == regionHistory,
                "same-day recapture must replace, not grow, region history");
            AssertTrue(IPoliticalSourceWorks(world.State), "observation interface");
        }

        static void TestDebugAdjustment()
        {
            var world = new HeadlessWorld();
            float step = world.State.Politics.Config.DebugAdjustmentMagnitude;
            AssertTrue(step == 10f, "debug step " + step);
            var before = SnapshotWorld(world);
            AssertTrue(PoliticsSystem.DebugAdjust(world.State, RegionId.Theocracy, RegionId.Empire, step), "+10");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == 10f, "+10 value");
            AssertTrue(PoliticsSystem.DebugAdjust(world.State, RegionId.Theocracy, RegionId.Empire, -step), "-10");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == 0f, "back to 0");
            var te = world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Empire);
            AssertTrue(te.History.Count == 2, "two debug entries");
            AssertTrue(te.History[0].Reason.Contains("Debug Adjustment"), te.History[0].Reason);
            AssertUnchanged(before, SnapshotWorld(world), "debug adjust is observation-layer only");
        }

        static void TestReset()
        {
            var world = new HeadlessWorld();
            PoliticsSystem.DebugAdjust(world.State, RegionId.Theocracy, RegionId.Empire, 10f);
            PoliticsSystem.DebugAdjust(world.State, RegionId.Empire, RegionId.Sea, -10f);
            world.AdvanceDays(30);
            world.Reset();

            var cfg = PoliticalConfig.CreateDefault();
            AssertPair(world, RegionId.Theocracy, RegionId.Empire, LookupInitial(cfg, RegionId.Theocracy, RegionId.Empire));
            AssertPair(world, RegionId.Theocracy, RegionId.Sea, LookupInitial(cfg, RegionId.Theocracy, RegionId.Sea));
            AssertPair(world, RegionId.Empire, RegionId.Sea, LookupInitial(cfg, RegionId.Empire, RegionId.Sea));
            foreach (var relation in world.State.Politics.Relations)
            {
                AssertTrue(relation.History.Count == 0, relation.PairLabel + " history after reset");
                AssertTrue(relation.LastChangedDay == 0, relation.PairLabel + " day after reset");
            }
        }

        static void TestFastForward()
        {
            var world = new HeadlessWorld();
            PoliticsSystem.DebugAdjust(world.State, RegionId.Theocracy, RegionId.Empire, 20f);
            float te = Value(world, RegionId.Theocracy, RegionId.Empire);
            float ts = Value(world, RegionId.Theocracy, RegionId.Sea);
            float es = Value(world, RegionId.Empire, RegionId.Sea);
            int hist = world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Empire).History.Count;
            var beforeSource = SnapshotWorld(world);

            var ff = FastForwardSystem.FastForwardYears(world.State, world.Races, world.Config, 1);
            AssertTrue(ff.State != null && ff.State.TotalDays >= 360, "FF advanced clone");
            AssertTrue(ValueOf(ff.State, RegionId.Theocracy, RegionId.Empire) == te, "FF clone TE stable");
            AssertTrue(ValueOf(ff.State, RegionId.Theocracy, RegionId.Sea) == ts, "FF clone TS stable");
            AssertTrue(ValueOf(ff.State, RegionId.Empire, RegionId.Sea) == es, "FF clone ES stable");
            AssertTrue(ff.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Empire).History.Count == hist,
                "FF must not invent political history");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == te, "FF must not mutate source relations");
            AssertUnchanged(beforeSource, SnapshotWorld(world), "FF clone must not mutate source region stats");
        }

        static void TestP2BCompatibility()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            session.Capture(world.State);
            AssertTrue(session.Current.Find(RegionId.Empire).Population == world.Region(RegionId.Empire).Population,
                "snapshot still copies population");
            int count = session.History.Count(RegionId.Theocracy);

            PoliticsSystem.DebugAdjust(world.State, RegionId.Theocracy, RegionId.Empire, 10f);
            AssertTrue(session.History.Count(RegionId.Theocracy) == count, "politics must not append region history");
            AssertTrue(HistoryMetrics.DisplayName(HistoryMetric.Population) == "Population", "P2-B metrics unchanged");

            var source = PoliticalObservation.FromWorld(world.State);
            AssertTrue(source != null, "observation adapter");
            AssertTrue(source.FindRelation(RegionId.Theocracy, RegionId.Empire).RelationValue == 10f, "adapter read");
            AssertTrue(source.GetHistory(RegionId.Theocracy, RegionId.Empire).Count == 1, "adapter history");

            world.AdvanceDay();
            session.Capture(world.State);
            AssertTrue(session.History.Count(RegionId.Sea) == count + 1, "P2-B history still records ticks");
            AssertTrue(session.History.Find(RegionId.Sea, world.State.TotalDays).Region.Population
                == world.Region(RegionId.Sea).Population, "history still matches live region");
        }

        static void TestNoSideEffects()
        {
            var world = new HeadlessWorld();
            world.AdvanceDays(15);
            var before = SnapshotWorld(world);
            PoliticsSystem.DebugAdjust(world.State, RegionId.Theocracy, RegionId.Empire, 10f);
            PoliticsSystem.DebugAdjust(world.State, RegionId.Theocracy, RegionId.Sea, -10f);
            PoliticsSystem.AdjustRelation(world.State, RegionId.Empire, RegionId.Sea, 25f, "Diplomatic Incident");
            AssertUnchanged(before, SnapshotWorld(world), "politics must not affect pop/resources/society");
        }

        static void TestNanInfinity()
        {
            var world = new HeadlessWorld();
            float original = Value(world, RegionId.Theocracy, RegionId.Empire);
            AssertTrue(!PoliticsSystem.AdjustRelation(world.State, RegionId.Theocracy, RegionId.Empire, float.NaN, "bad"), "NaN delta");
            AssertTrue(!PoliticsSystem.AdjustRelation(world.State, RegionId.Theocracy, RegionId.Empire, float.PositiveInfinity, "bad"), "+Inf delta");
            AssertTrue(!PoliticsSystem.AdjustRelation(world.State, RegionId.Theocracy, RegionId.Empire, float.NegativeInfinity, "bad"), "-Inf delta");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == original, "value unchanged after non-finite");
            AssertTrue(PoliticsSystem.ClampValue(float.NaN, world.State.Politics.Config) == 0f, "clamp NaN");
            AssertTrue(PoliticsSystem.ClampValue(float.PositiveInfinity, world.State.Politics.Config) == 0f, "clamp +Inf");
            AssertTrue(PoliticsSystem.ClampValue(float.NegativeInfinity, world.State.Politics.Config) == 0f, "clamp -Inf");
            AssertTrue(PoliticsSystem.ResolveState(float.NaN, world.State.Politics.Config) == PoliticalRelationState.Neutral,
                "NaN state");
            AssertTrue(PoliticsSystem.IsFinite(Value(world, RegionId.Theocracy, RegionId.Empire)), "finite");
        }

        static void TestLongRun360()
        {
            RunLong(360);
        }

        static void TestLongRun3600()
        {
            RunLong(3600);
        }

        static void RunLong(int days)
        {
            var world = new HeadlessWorld();
            var initial = CopyRelations(world);
            world.AdvanceDays(days);
            AssertTrue(!world.State.HaltedOnNumericError, world.State.LastNumericError ?? "halt");
            AssertRelationsStable(world, initial, "daily " + days);
            AssertFinitePolitics(world.State);

            var worldFf = new HeadlessWorld();
            var ff = FastForwardSystem.FastForwardToTotalDay(worldFf.State, worldFf.Races, worldFf.Config, days);
            AssertTrue(ff.State.TotalDays == days, "FF day " + ff.State.TotalDays);
            AssertRelationsStable(ff.State, initial, "FF " + days);
            AssertFinitePolitics(ff.State);
            Console.WriteLine("  " + days + "d daily+FF relation values unchanged, finite");
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

            string freezeDiff = GitDiffNames(root, "main", FrozenRelPaths);
            AssertTrue(string.IsNullOrWhiteSpace(freezeDiff), "P2-A frozen files differ from main:\n" + freezeDiff);

            foreach (var rel in FrozenRelPaths)
            {
                string text = File.ReadAllText(Path.Combine(root, rel));
                AssertTrue(!text.Contains("PoliticsSystem"), rel + " must not call PoliticsSystem");
                AssertTrue(!text.Contains("PoliticalRelation"), rel + " must not reference PoliticalRelation");
            }

            string daily = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Simulation/Core/DailySimulation.cs"));
            AssertTrue(!daily.Contains("Politics"), "DailySimulation must not tick politics");

            var world = new HeadlessWorld();
            DailySimulation.SimulateDay(world.State, world.Races, world.Config, world.Rng);
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire)
                == LookupInitial(PoliticalConfig.CreateDefault(), RegionId.Theocracy, RegionId.Empire),
                "one daily tick must not drift relations");
            AssertTrue(world.State.TotalDays > 0, "daily pipeline still advances calendar");
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

        static void TestNoWarLogic()
        {
            string root = FindRepoRoot();
            string dir = Path.Combine(root, "Assets/Scripts/Simulation/Politics");
            foreach (var path in Directory.GetFiles(dir, "*.cs"))
            {
                string text = File.ReadAllText(path);
                foreach (var token in WarBannedTokens)
                {
                    AssertTrue(!text.Contains(token), path + " contains war token " + token);
                }
            }

            string system = File.ReadAllText(Path.Combine(dir, "PoliticsSystem.cs"));
            AssertTrue(!system.Contains("void TickDay"), "PoliticsSystem must not tick with the daily pipeline");
        }

        static bool IPoliticalSourceWorks(WorldState state)
        {
            IPoliticalHistorySource source = PoliticalObservation.FromWorld(state);
            return source != null && source.GetRelations().Count == 3;
        }

        static void PushTo(HeadlessWorld world, RegionId a, RegionId b, float target)
        {
            var relation = world.State.Politics.FindRelation(a, b);
            float delta = target - relation.RelationValue;
            if (Math.Abs(delta) < 0.0001f)
            {
                return;
            }

            AssertTrue(PoliticsSystem.AdjustRelation(world.State, a, b, delta, "Test Set"), "push " + a + "-" + b);
        }

        static void AssertPair(HeadlessWorld world, RegionId a, RegionId b, float expected)
        {
            var relation = world.State.Politics.FindRelation(a, b);
            AssertTrue(relation != null, "missing " + a + " ↔ " + b);
            AssertTrue(Math.Abs(relation.RelationValue - expected) < 0.0001f,
                a + " ↔ " + b + " value " + relation.RelationValue + " expected " + expected);
            AssertTrue(relation.RelationState == PoliticsSystem.ResolveState(relation.RelationValue, world.State.Politics.Config),
                a + " ↔ " + b + " state");
        }

        static float LookupInitial(PoliticalConfig cfg, RegionId a, RegionId b)
        {
            PoliticsSystem.Canonical(a, b, out RegionId s, out RegionId t);
            for (int i = 0; i < cfg.InitialPoliticalRelations.Length; i++)
            {
                var e = cfg.InitialPoliticalRelations[i];
                PoliticsSystem.Canonical(e.RegionA, e.RegionB, out RegionId es, out RegionId et);
                if (es == s && et == t)
                {
                    return e.RelationValue;
                }
            }

            return 0f;
        }

        static float Value(HeadlessWorld world, RegionId a, RegionId b) => ValueOf(world.State, a, b);

        static float ValueOf(WorldState state, RegionId a, RegionId b)
        {
            var relation = state.Politics.FindRelation(a, b);
            AssertTrue(relation != null, "missing " + a + " ↔ " + b);
            return relation.RelationValue;
        }

        static float[] CopyRelations(HeadlessWorld world)
        {
            return new[]
            {
                Value(world, RegionId.Theocracy, RegionId.Empire),
                Value(world, RegionId.Theocracy, RegionId.Sea),
                Value(world, RegionId.Empire, RegionId.Sea)
            };
        }

        static void AssertRelationsStable(HeadlessWorld world, float[] initial, string label)
        {
            AssertRelationsStable(world.State, initial, label);
        }

        static void AssertRelationsStable(WorldState state, float[] initial, string label)
        {
            AssertTrue(ValueOf(state, RegionId.Theocracy, RegionId.Empire) == initial[0], label + " TE drifted");
            AssertTrue(ValueOf(state, RegionId.Theocracy, RegionId.Sea) == initial[1], label + " TS drifted");
            AssertTrue(ValueOf(state, RegionId.Empire, RegionId.Sea) == initial[2], label + " ES drifted");
            foreach (var relation in state.Politics.Relations)
            {
                AssertTrue(relation.History.Count == 0, label + " unexpected history on " + relation.PairLabel);
            }
        }

        static void AssertFinitePolitics(WorldState state)
        {
            foreach (var relation in state.Politics.Relations)
            {
                AssertTrue(PoliticsSystem.IsFinite(relation.RelationValue), relation.PairLabel + " non-finite " + relation.RelationValue);
                AssertTrue(relation.RelationState != PoliticalRelationState.War, relation.PairLabel + " war assigned");
            }
        }

        struct RegionSnap
        {
            public RegionId Id;
            public float Population;
            public float Food;
            public float Water;
            public float Wood;
            public float Mineral;
            public float Magic;
            public float FaithResource;
            public float Knowledge;
            public float Disease;
            public float Stability;
            public float Education;
            public float Faith;
        }

        struct WorldSnap
        {
            public RegionSnap[] Regions;
        }

        static WorldSnap SnapshotWorld(HeadlessWorld world)
        {
            var regions = world.State.Regions;
            var snap = new WorldSnap { Regions = new RegionSnap[regions.Length] };
            for (int i = 0; i < regions.Length; i++)
            {
                var r = regions[i];
                snap.Regions[i] = new RegionSnap
                {
                    Id = r.Id,
                    Population = r.Population,
                    Food = r.Get(ResourceId.Food),
                    Water = r.Get(ResourceId.Water),
                    Wood = r.Get(ResourceId.Timber),
                    Mineral = r.Get(ResourceId.Ore),
                    Magic = r.Get(ResourceId.Magic),
                    FaithResource = r.Get(ResourceId.Faith),
                    Knowledge = r.Get(ResourceId.Knowledge),
                    Disease = r.DiseasePressure,
                    Stability = r.Stability,
                    Education = r.Education,
                    Faith = r.FaithLevel
                };
            }

            return snap;
        }

        static void AssertUnchanged(WorldSnap before, WorldSnap after, string label)
        {
            AssertTrue(before.Regions.Length == after.Regions.Length, label + " region count");
            for (int i = 0; i < before.Regions.Length; i++)
            {
                var b = before.Regions[i];
                var a = after.Regions[i];
                AssertTrue(b.Id == a.Id, label + " id");
                AssertTrue(b.Population == a.Population, label + " " + b.Id + " Population");
                AssertTrue(b.Food == a.Food, label + " " + b.Id + " Food");
                AssertTrue(b.Water == a.Water, label + " " + b.Id + " Water");
                AssertTrue(b.Wood == a.Wood, label + " " + b.Id + " Wood");
                AssertTrue(b.Mineral == a.Mineral, label + " " + b.Id + " Mineral");
                AssertTrue(b.Magic == a.Magic, label + " " + b.Id + " Magic");
                AssertTrue(b.FaithResource == a.FaithResource, label + " " + b.Id + " Faith resource");
                AssertTrue(b.Knowledge == a.Knowledge, label + " " + b.Id + " Knowledge");
                AssertTrue(b.Disease == a.Disease, label + " " + b.Id + " Disease");
                AssertTrue(b.Stability == a.Stability, label + " " + b.Id + " Stability");
                AssertTrue(b.Education == a.Education, label + " " + b.Id + " Education");
                AssertTrue(b.Faith == a.Faith, label + " " + b.Id + " Faith");
            }
        }

        static string Sha256File(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(stream);
                var sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }

                return sb.ToString();
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
