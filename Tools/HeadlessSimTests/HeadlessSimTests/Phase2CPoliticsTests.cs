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
    /// P2-C v0.2: diplomatic actions on undirected political relations.
    /// Does not modify P2-A math or P2-B history buffers.
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
            Console.WriteLine("Divine World P2-C v0.2 Diplomatic Actions Foundation");
            Console.WriteLine("====================================================");

            var failures = new List<string>();
            Run("HudTitle_Is_P2C_Politics_v0.2", TestHudTitle, failures);
            Run("ImproveRelations", TestImproveRelations, failures);
            Run("WorsenRelations", TestWorsenRelations, failures);
            Run("RelationValue_Clamp", TestValueRange, failures);
            Run("RelationState", TestStateThreshold, failures);
            Run("SourceTarget_History", TestSourceTargetHistory, failures);
            Run("PoliticalHistory", TestHistory, failures);
            Run("DiplomaticIncident", TestDiplomaticIncident, failures);
            Run("Treaty_Creation_Expiry", TestTreaty, failures);
            Run("PairIsolation", TestPairIsolation, failures);
            Run("Reset", TestReset, failures);
            Run("FastForward", TestFastForward, failures);
            Run("PopulationSafety", TestPopulationSafety, failures);
            Run("ResourceSafety", TestResourceSafety, failures);
            Run("P2B_Compatibility", TestP2BCompatibility, failures);
            Run("NaN_Infinity", TestNanInfinity, failures);
            Run("P2A_Freeze", TestP2AFreeze, failures);
            Run("ProjectSettings", TestProjectSettings, failures);
            Run("Package", TestPackage, failures);
            Run("LongRun_360", TestLongRun360, failures);
            Run("LongRun_3600", TestLongRun3600, failures);
            Run("War_And_Peace_Reserved", TestWarReserved, failures);
            Run("UnifiedEntry_ApplyDiplomaticAction", TestUnifiedEntry, failures);
            Run("NoWarLogic", TestNoWarLogic, failures);
            Run("ResourceBaseline_RecordOnly", TestResourceBaseline, failures);
            Run("InitialRelations", TestInitialRelations, failures);
            Run("RelationSymmetry", TestSymmetry, failures);

            Console.WriteLine();
            const int total = 27;
            Console.WriteLine($"Result: {total - failures.Count}/{total} passed");
            foreach (var f in failures)
            {
                Console.WriteLine("FAIL: " + f);
            }

            Console.WriteLine();
            Console.WriteLine(failures.Count == 0
                ? "P2-C v0.2 AUTOMATED TEST = PASS"
                : "P2-C v0.2 AUTOMATED TEST = FAIL");

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
            AssertTrue(PoliticsVersion.HudTitle == "P2-C · Politics v0.2", PoliticsVersion.HudTitle);
            AssertTrue(PoliticsVersion.Number == "v0.2", PoliticsVersion.Number);
            AssertTrue(ObservationVersion.HudTitle == "P2-B · Observation v0.5", ObservationVersion.HudTitle);

            string root = FindRepoRoot();
            string versionFile = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Simulation/Politics/PoliticsVersion.cs"));
            AssertTrue(versionFile.Contains("P2-C · Politics v0.2"), "version constant must be the full title");
            string hud = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Simulation/UI/PoliticsHud.cs"));
            AssertTrue(hud.Contains("PoliticsVersion.HudTitle"), "HUD must display PoliticsVersion.HudTitle");
            AssertTrue(hud.Contains("改善关系"), "improve button");
            AssertTrue(hud.Contains("恶化关系"), "worsen button");
            AssertTrue(!hud.Contains("P2-C · Politics v0.1"), "HUD must not keep v0.1 title");
        }

        static void TestImproveRelations()
        {
            var world = new HeadlessWorld();
            var before = SnapshotWorld(world);
            AssertTrue(PoliticsSystem.ImproveRelations(
                world.State,
                RegionId.Empire,
                RegionId.Theocracy,
                10f,
                "Diplomatic Gesture"), "improve");

            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == 10f, "TE = +10");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Sea) == 0f, "TS unchanged");
            AssertTrue(Value(world, RegionId.Empire, RegionId.Sea) == 0f, "ES unchanged");

            var te = world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Empire);
            AssertTrue(te.RelationState == PoliticalRelationState.Neutral, "10 is Neutral");
            AssertTrue(te.History.Count == 1, "one pair history");
            AssertTrue(te.History[0].ActionType == DiplomaticActionType.ImproveRelations, "type");
            AssertTrue(te.History[0].Reason == "Diplomatic Gesture", te.History[0].Reason);
            AssertTrue(world.State.Politics.GetDiplomaticHistory().Count == 1, "diplomatic log");
            AssertUnchanged(before, SnapshotWorld(world), "improve must not touch region stats");

            AssertTrue(PoliticsSystem.WorsenRelations(
                world.State,
                RegionId.Empire,
                RegionId.Theocracy,
                20f,
                "Diplomatic Slight"), "worsen after improve");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == -10f, "TE after -20");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Sea) == 0f, "TS still unchanged");
            AssertTrue(Value(world, RegionId.Empire, RegionId.Sea) == 0f, "ES still unchanged");

            AssertTrue(PoliticsSystem.ImproveRelations(
                world.State,
                RegionId.Empire,
                RegionId.Theocracy,
                1000f,
                "Diplomatic Gesture"), "clamp up");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == 100f, "clamp +100");
            AssertTrue(PoliticsSystem.WorsenRelations(
                world.State,
                RegionId.Empire,
                RegionId.Theocracy,
                1000f,
                "Diplomatic Slight"), "clamp down");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == -100f, "clamp -100");
        }

        static void TestWorsenRelations()
        {
            var world = new HeadlessWorld();
            AssertTrue(PoliticsSystem.WorsenRelations(
                world.State,
                RegionId.Theocracy,
                RegionId.Empire,
                10f,
                "Diplomatic Slight"), "worsen");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == -10f, "TE -10");
            var entry = world.State.Politics.GetDiplomaticHistory()[0];
            AssertTrue(entry.ActionType == DiplomaticActionType.WorsenRelations, "type");
            AssertTrue(entry.Delta == -10f, "delta " + entry.Delta);
            AssertTrue(entry.OldValue == 0f && entry.NewValue == -10f, "old/new");
            AssertTrue(PoliticsSystem.WorsenRelations(
                world.State,
                RegionId.Theocracy,
                RegionId.Empire,
                -25f,
                "Diplomatic Slight"), "negative magnitude still worsens");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == -35f, "abs magnitude");
        }

        static void TestValueRange()
        {
            var world = new HeadlessWorld();
            var cfg = world.State.Politics.Config;
            AssertTrue(PoliticsSystem.ImproveRelations(
                world.State, RegionId.Theocracy, RegionId.Empire, 1000f, "Diplomatic Gesture"), "+1000");
            var te = world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Empire);
            AssertTrue(te.RelationValue == cfg.MaxRelationValue, "upper clamp " + te.RelationValue);
            AssertTrue(te.RelationValue <= 100f && te.RelationValue >= -100f, "range");

            world.Reset();
            AssertTrue(PoliticsSystem.WorsenRelations(
                world.State, RegionId.Theocracy, RegionId.Empire, 1000f, "Diplomatic Slight"), "-1000");
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
            PoliticsSystem.ImproveRelations(world.State, RegionId.Theocracy, RegionId.Sea, 60f, "Diplomatic Gesture");
            var ts = world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Sea);
            AssertTrue(ts.RelationState == PoliticalRelationState.Friendly, "state derived from value");
            AssertTrue(ts.RelationState == PoliticsSystem.ResolveState(ts.RelationValue, cfg), "never assigned from action type");
        }

        static void TestSourceTargetHistory()
        {
            var world = new HeadlessWorld();
            world.AdvanceDays(120);
            AssertTrue(PoliticsSystem.ImproveRelations(
                world.State,
                RegionId.Empire,
                RegionId.Theocracy,
                10f,
                "Diplomatic Gesture"), "empire toward theocracy");

            var history = world.State.Politics.GetHistory(RegionId.Theocracy, RegionId.Empire);
            AssertTrue(history.Count == 1, "one entry");
            var entry = history[0];
            AssertTrue(entry.Day == 120, "day " + entry.Day);
            AssertTrue(entry.SourceRegionId == RegionId.Empire, "action source Empire");
            AssertTrue(entry.TargetRegionId == RegionId.Theocracy, "action target Theocracy");
            AssertTrue(entry.ActionType == DiplomaticActionType.ImproveRelations, "type");
            AssertTrue(entry.OldValue == 0f && entry.Delta == 10f && entry.NewValue == 10f, "old/delta/new");

            var pair = world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Empire);
            AssertTrue(pair.SourceRegionId == RegionId.Theocracy && pair.TargetRegionId == RegionId.Empire,
                "undirected pair stays canonical");
            AssertTrue(pair.RelationValue == 10f, "undirected value");

            string line = entry.ToObservationLine();
            AssertTrue(line.Contains("Day 120"), line);
            AssertTrue(line.Contains("Empire → Theocracy"), line);
            AssertTrue(line.Contains("ImproveRelations"), line);
            AssertTrue(line.Contains("Diplomatic Gesture"), line);
        }

        static void TestHistory()
        {
            var cfg = PoliticalConfig.CreateDefault();
            cfg.InitialPoliticalRelations[0].RelationValue = 30f;
            var world = new HeadlessWorld();
            world.State.Politics = PoliticsSystem.CreateInitialState(cfg);
            world.AdvanceDays(120);
            var session = new ObservationSession();
            session.Capture(world.State);
            int regionHistory = session.History.TotalEntryCount();

            AssertTrue(PoliticsSystem.ImproveRelations(world.State, RegionId.Theocracy, RegionId.Empire, 10f, "Diplomatic Gesture"), "+10");
            AssertTrue(PoliticsSystem.WorsenRelations(world.State, RegionId.Theocracy, RegionId.Empire, 20f, "Diplomatic Slight"), "-20");
            AssertTrue(PoliticsSystem.ImproveRelations(world.State, RegionId.Theocracy, RegionId.Empire, 15f, "Diplomatic Gesture"), "+15");

            var history = world.State.Politics.GetHistory(RegionId.Theocracy, RegionId.Empire);
            AssertTrue(history.Count == 3, "three relation changes");
            AssertTrue(history[0].OldValue == 30f && history[0].Delta == 10f && history[0].NewValue == 40f, "+10 row");
            AssertTrue(history[1].OldValue == 40f && history[1].Delta == -20f && history[1].NewValue == 20f, "-20 row");
            AssertTrue(history[2].OldValue == 20f && history[2].Delta == 15f && history[2].NewValue == 35f, "+15 row");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == 35f, "final 35");
            AssertTrue(world.State.Politics.GetDiplomaticHistory().Count == 3, "chronological diplomatic log");

            AssertTrue(session.History.TotalEntryCount() == regionHistory,
                "political history must not write ObservationHistory without recapture");
            session.Capture(world.State);
            AssertTrue(session.History.TotalEntryCount() == regionHistory,
                "same-day recapture must replace, not grow, region history");
            AssertTrue(IPoliticalSourceWorks(world.State), "observation interface");
        }

        static void TestDiplomaticIncident()
        {
            var world = new HeadlessWorld();
            world.AdvanceDays(40);
            AssertTrue(world.State.Politics.GetDiplomaticHistory().Count == 0, "no auto incidents");

            var incident = new DiplomaticIncident
            {
                Type = DiplomaticIncidentType.BorderTension,
                SourceRegion = RegionId.Empire,
                TargetRegion = RegionId.Sea,
                Delta = -20f,
                Reason = "Border Tension"
            };
            var before = SnapshotWorld(world);
            AssertTrue(PoliticsSystem.ApplyDiplomaticIncident(world.State, incident), "apply incident");
            AssertTrue(incident.Day == 40, "incident day " + incident.Day);
            AssertTrue(Value(world, RegionId.Empire, RegionId.Sea) == -20f, "ES changed");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == 0f, "TE isolated");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Sea) == 0f, "TS isolated");

            var entry = world.State.Politics.GetDiplomaticHistory()[0];
            AssertTrue(entry.ActionType == DiplomaticActionType.DiplomaticIncident, "type");
            AssertTrue(entry.SourceRegionId == RegionId.Empire && entry.TargetRegionId == RegionId.Sea, "direction");
            AssertTrue(entry.Reason == "Border Tension", entry.Reason);
            AssertUnchanged(before, SnapshotWorld(world), "incident must not touch pop/resources");

            world.AdvanceDays(20);
            AssertTrue(world.State.Politics.GetDiplomaticHistory().Count == 1, "still no auto-generated incidents");
        }

        static void TestTreaty()
        {
            var world = new HeadlessWorld();
            var before = SnapshotWorld(world);
            float te = Value(world, RegionId.Theocracy, RegionId.Empire);
            var treaty = PoliticsSystem.CreateTreaty(
                world.State,
                TreatyType.NonAggression,
                RegionId.Empire,
                RegionId.Theocracy,
                10,
                "Non-Aggression Pact");
            AssertTrue(treaty != null, "created");
            AssertTrue(treaty.TreatyType == TreatyType.NonAggression, "type");
            AssertTrue(treaty.SourceRegion == RegionId.Empire && treaty.TargetRegion == RegionId.Theocracy, "direction");
            AssertTrue(treaty.StartDay == 0 && treaty.EndDay == 10, "span");
            AssertTrue(treaty.IsActiveAt(0), "active at start");
            AssertTrue(PoliticsSystem.GetActiveTreaties(world.State).Count == 1, "active list");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == te, "treaty must not change relation");
            AssertUnchanged(before, SnapshotWorld(world), "treaty placeholder has no resource/pop effect");

            var trade = PoliticsSystem.CreateTreaty(
                world.State, TreatyType.Trade, RegionId.Empire, RegionId.Sea, 30, "Trade Placeholder");
            AssertTrue(trade != null, "trade placeholder");
            AssertUnchanged(before, SnapshotWorld(world), "Trade treaty must not move resources");

            var alliance = PoliticsSystem.CreateTreaty(
                world.State, TreatyType.Alliance, RegionId.Theocracy, RegionId.Sea, 30, "Alliance Placeholder");
            AssertTrue(alliance != null, "alliance placeholder");
            AssertTrue(world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Sea).RelationState
                != PoliticalRelationState.War, "alliance is not war");

            world.AdvanceDays(11);
            AssertTrue(!treaty.IsActiveAt(world.State.TotalDays), "calendar expiry");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == te, "expiry does not drift relation");

            AssertTrue(PoliticsSystem.ExpireTreaty(world.State, trade, "Treaty Expired"), "explicit expire");
            AssertTrue(!trade.IsActiveAt(world.State.TotalDays), "explicit inactive");
            AssertTrue(!trade.Active, "Active cleared");

            var ffWorld = new HeadlessWorld();
            PoliticsSystem.CreateTreaty(ffWorld.State, TreatyType.NonAggression, RegionId.Empire, RegionId.Theocracy, 10, "FF treaty");
            float ffTe = Value(ffWorld, RegionId.Theocracy, RegionId.Empire);
            var ff = FastForwardSystem.FastForwardToTotalDay(ffWorld.State, ffWorld.Races, ffWorld.Config, 360);
            AssertTrue(!ff.State.Politics.Treaties[0].IsActiveAt(ff.State.TotalDays), "FF calendar expiry");
            AssertTrue(ValueOf(ff.State, RegionId.Theocracy, RegionId.Empire) == ffTe, "FF treaty does not change relation");
        }

        static void TestPairIsolation()
        {
            var world = new HeadlessWorld();
            var before = SnapshotWorld(world);
            float ts = Value(world, RegionId.Theocracy, RegionId.Sea);
            float es = Value(world, RegionId.Empire, RegionId.Sea);

            AssertTrue(PoliticsSystem.ImproveRelations(
                world.State, RegionId.Theocracy, RegionId.Empire, 10f, "Diplomatic Gesture"), "adjust TE");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == 10f, "TE changed");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Sea) == ts, "TS must not change");
            AssertTrue(Value(world, RegionId.Empire, RegionId.Sea) == es, "ES must not change");
            AssertUnchanged(before, SnapshotWorld(world), "pair isolation must not touch region stats");
        }

        static void TestReset()
        {
            var world = new HeadlessWorld();
            PoliticsSystem.ImproveRelations(world.State, RegionId.Theocracy, RegionId.Empire, 10f, "Diplomatic Gesture");
            PoliticsSystem.WorsenRelations(world.State, RegionId.Empire, RegionId.Sea, 10f, "Diplomatic Slight");
            PoliticsSystem.CreateTreaty(world.State, TreatyType.NonAggression, RegionId.Theocracy, RegionId.Sea, 90, "Reset me");
            world.AdvanceDays(30);
            world.Reset();

            var cfg = PoliticalConfig.CreateDefault();
            AssertPair(world, RegionId.Theocracy, RegionId.Empire, LookupInitial(cfg, RegionId.Theocracy, RegionId.Empire));
            AssertPair(world, RegionId.Theocracy, RegionId.Sea, LookupInitial(cfg, RegionId.Theocracy, RegionId.Sea));
            AssertPair(world, RegionId.Empire, RegionId.Sea, LookupInitial(cfg, RegionId.Empire, RegionId.Sea));
            AssertTrue(world.State.Politics.GetDiplomaticHistory().Count == 0, "diplomatic history cleared");
            AssertTrue(world.State.Politics.GetTreaties().Count == 0, "treaties cleared");
            foreach (var relation in world.State.Politics.Relations)
            {
                AssertTrue(relation.History.Count == 0, relation.PairLabel + " history after reset");
                AssertTrue(relation.LastChangedDay == 0, relation.PairLabel + " day after reset");
            }
        }

        static void TestFastForward()
        {
            var world = new HeadlessWorld();
            PoliticsSystem.ImproveRelations(world.State, RegionId.Theocracy, RegionId.Empire, 20f, "Diplomatic Gesture");
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

        static void TestPopulationSafety()
        {
            var world = new HeadlessWorld();
            world.AdvanceDays(15);
            var before = SnapshotWorld(world);
            PoliticsSystem.ImproveRelations(world.State, RegionId.Theocracy, RegionId.Empire, 10f, "Diplomatic Gesture");
            PoliticsSystem.WorsenRelations(world.State, RegionId.Theocracy, RegionId.Sea, 10f, "Diplomatic Slight");
            PoliticsSystem.ApplyDiplomaticIncident(world.State, new DiplomaticIncident
            {
                Type = DiplomaticIncidentType.Unspecified,
                SourceRegion = RegionId.Empire,
                TargetRegion = RegionId.Sea,
                Delta = 25f,
                Reason = "Diplomatic Incident"
            });
            var after = SnapshotWorld(world);
            AssertUnchanged(before, after, "diplomacy must not affect population/society");
            for (int i = 0; i < before.Regions.Length; i++)
            {
                AssertTrue(before.Regions[i].Population == after.Regions[i].Population, "Population");
                AssertTrue(before.Regions[i].Disease == after.Regions[i].Disease, "Disease");
                AssertTrue(before.Regions[i].Stability == after.Regions[i].Stability, "Stability");
                AssertTrue(before.Regions[i].Education == after.Regions[i].Education, "Education");
                AssertTrue(before.Regions[i].Faith == after.Regions[i].Faith, "Faith");
            }
        }

        static void TestResourceSafety()
        {
            var world = new HeadlessWorld();
            var before = SnapshotWorld(world);
            float te = Value(world, RegionId.Theocracy, RegionId.Empire);
            world.Region(RegionId.Theocracy).Set(ResourceId.Food, 1f);
            world.Region(RegionId.Empire).Set(ResourceId.Water, 1f);
            world.Region(RegionId.Sea).Set(ResourceId.Timber, 1f);
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == te, "resources must not change relations");

            world.Reset();
            before = SnapshotWorld(world);
            PoliticsSystem.ImproveRelations(world.State, RegionId.Theocracy, RegionId.Empire, 10f, "Diplomatic Gesture");
            PoliticsSystem.CreateTreaty(world.State, TreatyType.Trade, RegionId.Empire, RegionId.Sea, 90, "Trade Placeholder");
            AssertUnchanged(before, SnapshotWorld(world), "diplomacy/treaty must not move resources");
            AssertTrue(ResourceDiplomacyBaseline.IsRecordOnly(), "baseline must stay record-only");
        }

        static void TestP2BCompatibility()
        {
            var world = new HeadlessWorld();
            var session = new ObservationSession();
            session.Capture(world.State);
            AssertTrue(session.Current.Find(RegionId.Empire).Population == world.Region(RegionId.Empire).Population,
                "snapshot still copies population");
            int count = session.History.Count(RegionId.Theocracy);

            PoliticsSystem.ImproveRelations(world.State, RegionId.Theocracy, RegionId.Empire, 10f, "Diplomatic Gesture");
            AssertTrue(session.History.Count(RegionId.Theocracy) == count, "politics must not append region history");
            AssertTrue(HistoryMetrics.DisplayName(HistoryMetric.Population) == "Population", "P2-B metrics unchanged");

            var source = PoliticalObservation.FromWorld(world.State);
            AssertTrue(source != null, "observation adapter");
            AssertTrue(source.FindRelation(RegionId.Theocracy, RegionId.Empire).RelationValue == 10f, "adapter read");
            AssertTrue(source.GetHistory(RegionId.Theocracy, RegionId.Empire).Count == 1, "adapter history");
            AssertTrue(source.GetDiplomaticHistory().Count == 1, "adapter diplomatic history");
            AssertTrue(source.GetTreaties().Count == 0, "adapter treaties");

            world.AdvanceDay();
            session.Capture(world.State);
            AssertTrue(session.History.Count(RegionId.Sea) == count + 1, "P2-B history still records ticks");
            AssertTrue(session.History.Find(RegionId.Sea, world.State.TotalDays).Region.Population
                == world.Region(RegionId.Sea).Population, "history still matches live region");
        }

        static void TestNanInfinity()
        {
            var world = new HeadlessWorld();
            float original = Value(world, RegionId.Theocracy, RegionId.Empire);
            AssertTrue(!PoliticsSystem.ApplyDiplomaticAction(world.State, DiplomaticAction.Create(
                RegionId.Theocracy, RegionId.Empire, DiplomaticActionType.ImproveRelations, float.NaN, "bad")), "NaN delta");
            AssertTrue(!PoliticsSystem.ApplyDiplomaticAction(world.State, DiplomaticAction.Create(
                RegionId.Theocracy, RegionId.Empire, DiplomaticActionType.ImproveRelations, float.PositiveInfinity, "bad")), "+Inf delta");
            AssertTrue(!PoliticsSystem.ApplyDiplomaticAction(world.State, DiplomaticAction.Create(
                RegionId.Theocracy, RegionId.Empire, DiplomaticActionType.WorsenRelations, float.NegativeInfinity, "bad")), "-Inf delta");
            AssertTrue(!PoliticsSystem.ImproveRelations(world.State, RegionId.Theocracy, RegionId.Empire, float.NaN, "bad"), "NaN improve");
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
            AssertTrue(world.State.Politics.GetDiplomaticHistory().Count == 0, "daily " + days + " no auto diplomacy");
            AssertFinitePolitics(world.State);

            var worldFf = new HeadlessWorld();
            var ff = FastForwardSystem.FastForwardToTotalDay(worldFf.State, worldFf.Races, worldFf.Config, days);
            AssertTrue(ff.State.TotalDays == days, "FF day " + ff.State.TotalDays);
            AssertRelationsStable(ff.State, initial, "FF " + days);
            AssertTrue(ff.State.Politics.GetDiplomaticHistory().Count == 0, "FF " + days + " no auto diplomacy");
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
                AssertTrue(!text.Contains("ApplyDiplomaticAction"), rel + " must not call diplomatic actions");
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

        static void TestProjectSettings()
        {
            string root = FindRepoRoot();
            string rel = "ProjectSettings/ProjectVersion.txt";
            string path = Path.Combine(root, rel);
            AssertTrue(File.Exists(path), "missing " + rel);
            string hash = Sha256File(path);
            AssertTrue(hash == "b42279cfd794d9f1825f3b7c1f318b861fa9e2e2b3c6c146737bdbd41c01b389",
                rel + " changed. expected b42279cfd794d9f1825f3b7c1f318b861fa9e2e2b3c6c146737bdbd41c01b389 got " + hash);

            string version = File.ReadAllText(path);
            AssertTrue(version.Contains("2022.3.62f3c1"), "Unity version");
            AssertTrue(version.Contains("1623fc0bbb97"), "Unity revision");
            string diff = GitDiffNames(root, "main", "ProjectSettings");
            AssertTrue(string.IsNullOrWhiteSpace(diff), "ProjectSettings differ from main:\n" + diff);
        }

        static void TestPackage()
        {
            string root = FindRepoRoot();
            var hashed = new (string RelPath, string Sha256)[]
            {
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

            string diff = GitDiffNames(root, "main", "Packages");
            AssertTrue(string.IsNullOrWhiteSpace(diff), "Packages differ from main:\n" + diff);
        }

        static void TestWarReserved()
        {
            AssertTrue(!WarReservation.Implemented, "war not implemented");
            AssertTrue(WarReservation.Status == "Reserved / NotImplemented", WarReservation.Status);
            AssertTrue(!PeaceReservation.Implemented, "peace not implemented");
            AssertTrue(PeaceReservation.Status == "Reserved / NotImplemented", PeaceReservation.Status);
            var cfg = PoliticalConfig.CreateDefault();
            for (float v = -100f; v <= 100f; v += 1f)
            {
                AssertTrue(PoliticsSystem.ResolveState(v, cfg) != PoliticalRelationState.War,
                    "War assigned at " + v);
            }
        }

        static void TestUnifiedEntry()
        {
            string root = FindRepoRoot();
            string dir = Path.Combine(root, "Assets/Scripts/Simulation/Politics");
            foreach (var path in Directory.GetFiles(dir, "*.cs"))
            {
                string text = File.ReadAllText(path);
                AssertTrue(!text.Contains("RelationValue +="), path + " must not add to RelationValue directly");
                AssertTrue(!text.Contains("RelationValue -="), path + " must not subtract from RelationValue directly");
            }

            var world = new HeadlessWorld();
            AssertTrue(PoliticsSystem.ApplyDiplomaticAction(world.State, DiplomaticAction.Create(
                RegionId.Empire,
                RegionId.Theocracy,
                DiplomaticActionType.ImproveRelations,
                10f,
                "Diplomatic Gesture")), "unified entry");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == 10f, "unified write");
            AssertTrue(PoliticsSystem.DebugAdjust(world.State, RegionId.Theocracy, RegionId.Empire, 10f), "debug uses entry");
            AssertTrue(Value(world, RegionId.Theocracy, RegionId.Empire) == 20f, "debug");
            AssertTrue(PoliticsSystem.AdjustRelation(
                world.State, RegionId.Theocracy, RegionId.Empire, -5f, "Diplomatic Incident"), "compat wrapper");
            AssertTrue(world.State.Politics.GetDiplomaticHistory()[2].ActionType
                == DiplomaticActionType.DiplomaticIncident, "incident inferred");
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

        static void TestResourceBaseline()
        {
            AssertTrue(!ResourceDiplomacyBaseline.CoupledToDiplomacy, "not coupled");
            AssertTrue(!ResourceDiplomacyBaseline.ResourceTradeImplemented, "no trade");
            AssertTrue(!string.IsNullOrEmpty(ResourceDiplomacyBaseline.Water), "water recorded");
            AssertTrue(!string.IsNullOrEmpty(ResourceDiplomacyBaseline.Food), "food recorded");
            AssertTrue(!string.IsNullOrEmpty(ResourceDiplomacyBaseline.Faith), "faith recorded");
            AssertTrue(!string.IsNullOrEmpty(ResourceDiplomacyBaseline.Wood), "wood recorded");
            AssertTrue(!string.IsNullOrEmpty(ResourceDiplomacyBaseline.Mineral), "mineral recorded");
            AssertTrue(!string.IsNullOrEmpty(ResourceDiplomacyBaseline.Magic), "magic recorded");
            AssertTrue(!string.IsNullOrEmpty(ResourceDiplomacyBaseline.KnowledgeEducation), "knowledge recorded");

            string root = FindRepoRoot();
            string system = File.ReadAllText(Path.Combine(root, "Assets/Scripts/Simulation/Politics/PoliticsSystem.cs"));
            AssertTrue(!system.Contains("ResourceId"), "ApplyDiplomaticAction must not read ResourceId");
            AssertTrue(!system.Contains("ResourceDiplomacyBaseline"), "baseline must not be wired into PoliticsSystem");
            string doc = File.ReadAllText(Path.Combine(root, "docs/P2-C-v0.2-Resource-Design-Baseline.md"));
            AssertTrue(doc.Contains("禁止：资源 → 外交关系"), "design doc recorded");
        }

        static void TestInitialRelations()
        {
            var world = new HeadlessWorld();
            var politics = world.State.Politics;
            AssertTrue(politics != null, "Politics missing on new world");
            AssertTrue(politics.Relations != null && politics.Relations.Count == 3, "expected 3 pairs, got " + (politics.Relations?.Count ?? -1));
            AssertTrue(politics.DiplomaticHistory != null && politics.DiplomaticHistory.Count == 0, "diplomatic history empty");
            AssertTrue(politics.Treaties != null && politics.Treaties.Count == 0, "treaties empty");

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

            cfg.InitialPoliticalRelations[0].RelationValue = 12f;
            var fromConfig = PoliticsSystem.CreateInitialState(cfg);
            AssertTrue(fromConfig.FindRelation(RegionId.Theocracy, RegionId.Empire).RelationValue == 12f, "TE from config");
        }

        static void TestSymmetry()
        {
            var world = new HeadlessWorld();
            var ab = world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Empire);
            var ba = world.State.Politics.FindRelation(RegionId.Empire, RegionId.Theocracy);
            AssertTrue(ReferenceEquals(ab, ba), "undirected pair must be one object");
            AssertTrue(world.State.Politics.Relations.Count == 3, "no directed duplicates");

            PoliticsSystem.ImproveRelations(world.State, RegionId.Empire, RegionId.Theocracy, 10f, "Diplomatic Gesture");
            AssertTrue(ab.RelationValue == 10f, "directed action writes the same pair");
            AssertTrue(ba.RelationValue == 10f, "symmetric read");
            AssertTrue(world.State.Politics.FindRelation(RegionId.Theocracy, RegionId.Theocracy) == null, "no self pair");
        }

        static bool IPoliticalSourceWorks(WorldState state)
        {
            IPoliticalHistorySource source = PoliticalObservation.FromWorld(state);
            return source != null && source.GetRelations().Count == 3 && source.GetDiplomaticHistory() != null;
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
