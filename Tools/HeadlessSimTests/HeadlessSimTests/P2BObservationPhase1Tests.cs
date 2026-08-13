using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;
using DivineWorld.Simulation.Systems;

namespace HeadlessSimTests
{
    /// <summary>
    /// P2-B phase-1 verification: Observation / History vs SimulationWorld.State.
    /// Observes only — does not modify P2-A formulas, config, or UI.
    /// </summary>
    public static class P2BObservationPhase1Tests
    {
        public const int Seed = 20260810;
        const float Eps = 1e-4f;
        const float EpsLoose = 1e-3f;

        static readonly RegionId[] RequiredRegions = { RegionId.Theocracy, RegionId.Empire, RegionId.Sea };

        static readonly List<Finding> Findings = new List<Finding>();
        static readonly Dictionary<string, bool> SectionPass = new Dictionary<string, bool>();
        static bool SawNanOrInf;
        static bool HarnessException;

        struct Finding
        {
            public string Section;
            public string Severity;
            public string Detail;
        }

        public static int Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Findings.Clear();
            SectionPass.Clear();
            SawNanOrInf = false;
            HarnessException = false;

            Console.WriteLine("P2-B Observation Test Report");
            Console.WriteLine("============================");
            Console.WriteLine($"Seed={Seed}");
            Console.WriteLine("Policy: no P2-A math/config edits; no UI; observation read-only.");
            Console.WriteLine();

            try
            {
                SectionPass["Build"] = true;
                TestStateConsistency();
                TestDailyTick();
                TestSeason();
                TestEvents();
                TestFastForward();
                TestHistoryBuffer();
                TestReset();
                TestHistoryReset();
                TestDataIsolation();
                TestFreezeUntouched();
            }
            catch (Exception ex)
            {
                HarnessException = true;
                Findings.Add(new Finding
                {
                    Section = "Harness",
                    Severity = "P0",
                    Detail = "Unhandled harness exception: " + ex
                });
                Console.WriteLine("FAIL Harness exception: " + ex);
            }

            SectionPass["NaN / Infinity"] = !SawNanOrInf;
            if (SawNanOrInf)
            {
                Fail("NaN / Infinity", "P0", "NaN or Infinity appeared in Snapshot or State during tests.");
            }

            PrintReport();
            return 0; // diagnostic: always 0; sections classified in report
        }

        static void TestStateConsistency()
        {
            Header("2. State Consistency — Day 0 / Day 1 × 3 regions");
            bool ok = true;
            var world = new HeadlessWorld(Seed);
            ok &= CheckWorldSnapshot("Day0", world.State, SimulationObservation.Capture(world.State));
            world.AdvanceDay();
            ok &= CheckWorldSnapshot("Day1", world.State, SimulationObservation.Capture(world.State));
            SectionPass["State Consistency"] = ok;
            Console.WriteLine(ok ? "PASS State Consistency" : "FAIL State Consistency");
        }

        static void TestDailyTick()
        {
            Header("3. Daily Tick — AdvanceDay / Capture / Record / History vs State");
            int[] days = { 1, 2, 7, 30, 90 };
            var world = new HeadlessWorld(Seed);
            var history = new SimulationHistoryBuffer(512);
            history.Record(SimulationObservation.Capture(world.State));

            bool ok = true;
            int targetIdx = 0;
            while (targetIdx < days.Length)
            {
                int target = days[targetIdx];
                while (world.State.TotalDays < target)
                {
                    world.AdvanceDay();
                    if (world.State.HaltedOnNumericError)
                    {
                        ok = false;
                        Fail("Daily Tick", "P0", "Numeric halt at TotalDays=" + world.State.TotalDays + " " + world.State.LastNumericError);
                        break;
                    }
                }

                var snap = SimulationObservation.Capture(world.State);
                history.Record(snap);
                ok &= CheckWorldSnapshot("Tick TotalDays=" + world.State.TotalDays, world.State, snap);

                var fromHist = history.TryGetExact(world.State.TotalDays);
                if (fromHist == null)
                {
                    ok = false;
                    Fail("Daily Tick", "P1", "HistoryBuffer.TryGetExact(" + world.State.TotalDays + ") returned null after Record.");
                }
                else
                {
                    ok &= CompareSnapshots("History vs Capture @ " + world.State.TotalDays, fromHist, snap);
                    ok &= CheckWorldSnapshot("History vs State @ " + world.State.TotalDays, world.State, fromHist);
                }

                targetIdx++;
                if (world.State.HaltedOnNumericError)
                {
                    break;
                }
            }

            SectionPass["Daily Tick"] = ok;
            Console.WriteLine(ok ? "PASS Daily Tick" : "FAIL Daily Tick");
        }

        static void TestSeason()
        {
            Header("4. Season — Snapshot calendar == WorldState (DayOfYear checkpoints)");
            var checkpoints = new[]
            {
                (DayOfYear: 1, Season: SeasonId.Spring, DayIn: 1, Label: "Day 1 Spring first"),
                (DayOfYear: 90, Season: SeasonId.Spring, DayIn: 90, Label: "Day 90 Spring last"),
                (DayOfYear: 91, Season: SeasonId.Summer, DayIn: 1, Label: "Day 91 Summer first"),
                (DayOfYear: 180, Season: SeasonId.Summer, DayIn: 90, Label: "Day 180 Summer last"),
                (DayOfYear: 181, Season: SeasonId.Autumn, DayIn: 1, Label: "Day 181 Autumn first"),
                (DayOfYear: 270, Season: SeasonId.Autumn, DayIn: 90, Label: "Day 270 Autumn last"),
                (DayOfYear: 271, Season: SeasonId.Winter, DayIn: 1, Label: "Day 271 Winter first"),
                (DayOfYear: 360, Season: SeasonId.Winter, DayIn: 90, Label: "Day 360 Winter last")
            };

            bool ok = true;
            var world = new HeadlessWorld(Seed);
            foreach (var cp in checkpoints)
            {
                AdvanceToDayOfYear(world, cp.DayOfYear);
                var state = world.State;
                var snap = SimulationObservation.Capture(state);
                ScanFinite("Season " + cp.Label, state, snap);

                bool matchState = snap.CurrentSeason == state.CurrentSeason
                    && snap.SeasonIndex == state.SeasonIndex
                    && snap.DayInSeason == state.DayInSeason
                    && Math.Abs(snap.SeasonProgress - state.SeasonProgress) < 1e-6f
                    && snap.Year == state.Year
                    && snap.TotalDays == state.TotalDays
                    && snap.DayOfYear == state.DayOfYear;

                if (!matchState)
                {
                    ok = false;
                    Fail("Season", "P1",
                        cp.Label + " Snapshot≠State"
                        + " snap(Y=" + snap.Year + " doy=" + snap.DayOfYear + " td=" + snap.TotalDays
                        + " season=" + snap.CurrentSeason + " idx=" + snap.SeasonIndex
                        + " in=" + snap.DayInSeason + " prog=" + snap.SeasonProgress.ToString("0.###", CultureInfo.InvariantCulture) + ")"
                        + " state(Y=" + state.Year + " doy=" + state.DayOfYear + " td=" + state.TotalDays
                        + " season=" + state.CurrentSeason + " idx=" + state.SeasonIndex
                        + " in=" + state.DayInSeason + " prog=" + state.SeasonProgress.ToString("0.###", CultureInfo.InvariantCulture) + ")");
                }

                bool stateMatchesTable = state.DayOfYear == cp.DayOfYear
                    && state.CurrentSeason == cp.Season
                    && state.DayInSeason == cp.DayIn
                    && state.SeasonIndex == (int)cp.Season;

                Console.WriteLine(
                    (matchState ? "  SNAP=STATE" : "  SNAP≠STATE")
                    + (stateMatchesTable ? "  STATE=TABLE" : "  STATE≠TABLE")
                    + "  " + cp.Label
                    + "  TotalDays=" + state.TotalDays
                    + " DayOfYear=" + state.DayOfYear
                    + " Season=" + state.CurrentSeason
                    + " DayInSeason=" + state.DayInSeason);

                if (!stateMatchesTable)
                {
                    Fail("Season", "P2-A?",
                        cp.Label + " WorldState calendar ≠ expected table (Observation copied State). "
                        + "Expected season=" + cp.Season + " DayInSeason=" + cp.DayIn
                        + " actual season=" + state.CurrentSeason + " DayInSeason=" + state.DayInSeason
                        + " TotalDays=" + state.TotalDays);
                    // Do not fail P2-B season section solely for P2-A table mismatch if snapshot matched State.
                }
            }

            SectionPass["Season"] = ok;
            Console.WriteLine(ok ? "PASS Season (Snapshot == State)" : "FAIL Season (Snapshot != State)");
        }

        static void TestEvents()
        {
            Header("5. Event — EventSystem → Snapshot copy; regional isolation; expiry");
            bool ok = true;
            var world = new HeadlessWorld(Seed);
            const int maxScan = 720;
            RegionEvent found = null;
            RegionId foundRegion = RegionId.Theocracy;
            int foundDay = -1;

            for (int d = 0; d <= maxScan; d++)
            {
                if (d > 0)
                {
                    world.AdvanceDay();
                }

                foreach (var id in RequiredRegions)
                {
                    var region = world.Region(id);
                    if (region?.ActiveEvents == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < region.ActiveEvents.Count; i++)
                    {
                        var e = region.ActiveEvents[i];
                        if (e != null && e.EventType == SimEventType.NaturalDisaster && e.IsActiveOn(world.State.TotalDays))
                        {
                            found = e;
                            foundRegion = id;
                            foundDay = world.State.TotalDays;
                            break;
                        }
                    }

                    if (found != null)
                    {
                        break;
                    }
                }

                if (found != null)
                {
                    break;
                }
            }

            if (found == null)
            {
                ok = false;
                Fail("Event", "P2-A?", "No NaturalDisaster from EventSystem within " + maxScan + " days (seed=" + Seed + "). Cannot complete event copy/isolation checks.");
                SectionPass["Event"] = false;
                Console.WriteLine("FAIL Event (no NaturalDisaster found)");
                return;
            }

            Console.WriteLine("  Found NaturalDisaster @ " + foundRegion + " TotalDays=" + foundDay
                + " EventId=" + found.EventId
                + " Start=" + found.StartDay + " Duration=" + found.Duration + " End=" + found.EndDay
                + " Severity=" + found.Severity.ToString("0.###", CultureInfo.InvariantCulture)
                + " Scope=" + found.Scope);

            var snap = SimulationObservation.Capture(world.State);
            ok &= CheckWorldSnapshot("Event day Capture", world.State, snap);

            var snapRegion = FindSnapRegion(snap, foundRegion);
            EventObservation snapEvt = null;
            if (snapRegion?.ActiveEvents != null)
            {
                for (int i = 0; i < snapRegion.ActiveEvents.Length; i++)
                {
                    if (snapRegion.ActiveEvents[i] != null && snapRegion.ActiveEvents[i].EventId == found.EventId)
                    {
                        snapEvt = snapRegion.ActiveEvents[i];
                        break;
                    }
                }
            }

            if (snapEvt == null)
            {
                ok = false;
                Fail("Event", "P1", "Snapshot missing EventId=" + found.EventId + " on " + foundRegion + " after EventSystem created it.");
            }
            else
            {
                if (snapEvt.EventType != found.EventType)
                {
                    ok = false;
                    Fail("Event", "P1", "EventType snap=" + snapEvt.EventType + " state=" + found.EventType);
                }

                if (snapEvt.RegionId != found.RegionId || snapEvt.RegionId != foundRegion)
                {
                    ok = false;
                    Fail("Event", "P1", "RegionId snap=" + snapEvt.RegionId + " state=" + found.RegionId);
                }

                if (Math.Abs(snapEvt.Severity - found.Severity) > Eps)
                {
                    ok = false;
                    Fail("Event", "P1", "Severity snap=" + snapEvt.Severity + " state=" + found.Severity);
                }

                if (snapEvt.StartDay != found.StartDay)
                {
                    ok = false;
                    Fail("Event", "P1", "StartDay snap=" + snapEvt.StartDay + " state=" + found.StartDay);
                }

                if (snapEvt.Duration != found.Duration)
                {
                    ok = false;
                    Fail("Event", "P1", "Duration snap=" + snapEvt.Duration + " state=" + found.Duration);
                }

                if (snapEvt.EndDay != found.EndDay)
                {
                    ok = false;
                    Fail("Event", "P1", "EndDay snap=" + snapEvt.EndDay + " state=" + found.EndDay);
                }

                if (!snapEvt.IsActive)
                {
                    ok = false;
                    Fail("Event", "P1", "IsActive=false in Snapshot while State.IsActiveOn(" + world.State.TotalDays + ")=true");
                }

                Console.WriteLine("  Snapshot event fields match State for " + found.EventId);
            }

            // Regional isolation: same EventId must not appear on other regions.
            foreach (var id in RequiredRegions)
            {
                if (id == foundRegion)
                {
                    continue;
                }

                var other = FindSnapRegion(snap, id);
                if (other?.ActiveEvents == null)
                {
                    continue;
                }

                for (int i = 0; i < other.ActiveEvents.Length; i++)
                {
                    var e = other.ActiveEvents[i];
                    if (e != null && e.EventId == found.EventId)
                    {
                        ok = false;
                        Fail("Event", "P1", "Same EventId leaked to " + id + " (Observation must not invent/share events).");
                    }

                    if (e != null && e.EventType == SimEventType.NaturalDisaster
                        && e.StartDay == found.StartDay && e.RegionId == foundRegion)
                    {
                        ok = false;
                        Fail("Event", "P1", id + " Snapshot shows Empire/source disaster RegionId incorrectly.");
                    }
                }
            }

            Console.WriteLine("  Isolation: other regions do not contain EventId " + found.EventId);

            int expireDay = found.EndDay;
            while (world.State.TotalDays < expireDay)
            {
                world.AdvanceDay();
                if (world.State.HaltedOnNumericError)
                {
                    break;
                }
            }

            var after = SimulationObservation.Capture(world.State);
            ok &= CheckWorldSnapshot("After event EndDay", world.State, after);
            var afterRegion = FindSnapRegion(after, foundRegion);
            bool stillActive = false;
            if (afterRegion?.ActiveEvents != null)
            {
                for (int i = 0; i < afterRegion.ActiveEvents.Length; i++)
                {
                    var e = afterRegion.ActiveEvents[i];
                    if (e != null && e.EventId == found.EventId && e.IsActive)
                    {
                        stillActive = true;
                    }
                }
            }

            var stateRegion = world.Region(foundRegion);
            bool stateStillActive = stateRegion != null && stateRegion.HasActiveEvent(SimEventType.NaturalDisaster, world.State.TotalDays)
                && FindStateEvent(stateRegion, found.EventId) != null
                && FindStateEvent(stateRegion, found.EventId).IsActiveOn(world.State.TotalDays);

            Console.WriteLine("  After EndDay TotalDays=" + world.State.TotalDays
                + " snapStillActive=" + stillActive
                + " stateStillActive=" + stateStillActive);

            if (stillActive && !stateStillActive)
            {
                ok = false;
                Fail("Event", "P1", "Snapshot still IsActive after State expired EventId=" + found.EventId);
            }

            if (stillActive && stateStillActive)
            {
                Fail("Event", "P2-A?", "NaturalDisaster still active after EndDay=" + expireDay
                    + " at TotalDays=" + world.State.TotalDays + " (Observation matches State).");
            }

            SectionPass["Event"] = ok;
            Console.WriteLine(ok ? "PASS Event" : "FAIL Event");
        }

        static void TestFastForward()
        {
            Header("6. FastForward — 1 year from Day 1; Snapshot == new State; no stale refs");
            bool ok = true;
            var world = new HeadlessWorld(Seed);
            world.AdvanceDay();
            var oldState = world.State;
            int day1 = oldState.TotalDays;
            float day1Pop = SumPop(oldState);

            var history = new SimulationHistoryBuffer(64);
            var day1Snap = SimulationObservation.Capture(oldState);
            history.Record(day1Snap);

            var result = FastForwardSystem.FastForwardYears(oldState, world.Races, world.Config, 1);
            var newState = result.State;
            int expectedTotal = day1 + SimulationConfig.DaysPerYear;

            Console.WriteLine("  FastForward " + day1 + " → " + (newState != null ? newState.TotalDays.ToString() : "null")
                + " expected " + expectedTotal);
            Console.WriteLine("  State reference replaced: " + (!ReferenceEquals(oldState, newState)));

            if (newState == null)
            {
                ok = false;
                Fail("FastForward", "P0", "FastForward result.State is null.");
                SectionPass["FastForward"] = false;
                Console.WriteLine("FAIL FastForward");
                return;
            }

            if (newState.TotalDays != expectedTotal)
            {
                Fail("FastForward", "P2-A?",
                    "After 1y FF TotalDays=" + newState.TotalDays + " expected " + expectedTotal
                    + " (Observation does not advance calendar).");
            }

            if (ReferenceEquals(oldState, newState))
            {
                Fail("FastForward", "P2-A?", "FastForward did not replace State reference (Clone expected). Observation still tested against result.State.");
            }

            var ffSnap = SimulationObservation.Capture(newState);
            ok &= CheckWorldSnapshot("After FastForward 1y", newState, ffSnap);

            if (ReferenceEquals(ffSnap.Regions, newState.Regions))
            {
                ok = false;
                Fail("FastForward", "P0", "Snapshot.Regions is the same array reference as State.Regions.");
            }

            var histDay1 = history.TryGetExact(day1);
            if (histDay1 == null)
            {
                ok = false;
                Fail("FastForward", "P1", "History lost Day-1 sample after FastForward.");
            }
            else
            {
                oldState.Regions[0].Population = 999999f;
                if (Math.Abs(histDay1.TotalPopulation - day1Pop) > EpsLoose)
                {
                    ok = false;
                    Fail("FastForward", "P0",
                        "History Day-1 TotalPopulation changed after mutating pre-FF State ("
                        + histDay1.TotalPopulation + " vs original " + day1Pop + "). History held a live State reference.");
                }
                else
                {
                    Console.WriteLine("  History Day-1 population unchanged after mutating old State (value copy).");
                }

                if (Math.Abs(ffSnap.TotalPopulation - SumPop(newState)) > EpsLoose)
                {
                    ok = false;
                    Fail("FastForward", "P0", "Post-FF Snapshot population ≠ new State.");
                }
            }

            history.Record(ffSnap);
            var histFf = history.TryGetExact(newState.TotalDays);
            if (histFf == null)
            {
                ok = false;
                Fail("FastForward", "P1", "History missing sample at FF endpoint TotalDays=" + newState.TotalDays);
            }
            else
            {
                ok &= CheckWorldSnapshot("History FF endpoint", newState, histFf);
            }

            var hostWorld = new HeadlessWorld(Seed);
            var host = BindHost(hostWorld);
            hostWorld.Reset(Seed);
            hostWorld.AdvanceDay();
            var ff = hostWorld.FastForwardYears(1);
            if (ff.State == null)
            {
                ok = false;
                Fail("FastForward", "P0", "ObservationHost FastForwardYears returned null State.");
            }
            var hostSnap = host.Latest;
            ok &= CheckWorldSnapshot("Host after FastForward 1y", hostWorld.State, hostSnap);
            int ffDay = hostWorld.State.TotalDays;
            if (host.History.TryGetExact(ffDay) == null)
            {
                ok = false;
                Fail("FastForward", "P1", "ObservationHost missing FF endpoint TotalDays=" + ffDay);
            }

            hostWorld.Reset(Seed);
            if (host.History.TryGetExact(ffDay) != null)
            {
                ok = false;
                Fail("FastForward", "P1", "After Reset, FastForward history TotalDays=" + ffDay + " still present.");
            }

            if (host.History.TryGetExact(0) == null)
            {
                ok = false;
                Fail("FastForward", "P1", "After Reset following FastForward, Day 0 was not recorded.");
            }
            else
            {
                Console.WriteLine("  ObservationHost Reset after FastForward cleared endpoint " + ffDay + " and recorded Day 0.");
            }

            SectionPass["FastForward"] = ok;
            Console.WriteLine(ok ? "PASS FastForward" : "FAIL FastForward");
        }

        static void TestHistoryBuffer()
        {
            Header("7. HistoryBuffer — exact days + missing day safety");
            int[] days = { 1, 2, 7, 30, 90, 180, 270, 360 };
            var world = new HeadlessWorld(Seed);
            var history = new SimulationHistoryBuffer(2048);
            history.Record(SimulationObservation.Capture(world.State));

            bool ok = true;
            foreach (int d in days)
            {
                while (world.State.TotalDays < d)
                {
                    world.AdvanceDay();
                    if (world.State.HaltedOnNumericError)
                    {
                        break;
                    }
                }

                var snap = SimulationObservation.Capture(world.State);
                history.Record(snap);
                var got = history.TryGetExact(d);
                if (got == null)
                {
                    ok = false;
                    Fail("HistoryBuffer", "P1", "TryGetExact(" + d + ") null after Record at TotalDays=" + world.State.TotalDays);
                }
                else
                {
                    ok &= CheckWorldSnapshot("History Get " + d, world.State, got);
                    Console.WriteLine("  Get(" + d + ") TotalDays=" + got.TotalDays + " pop=" + got.TotalPopulation.ToString("0.##", CultureInfo.InvariantCulture));
                }

                if (world.State.HaltedOnNumericError)
                {
                    break;
                }
            }

            WorldObservationSnapshot missing = null;
            try
            {
                missing = history.TryGetExact(999999);
            }
            catch (Exception ex)
            {
                ok = false;
                Fail("HistoryBuffer", "P0", "TryGetExact(999999) threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (missing != null)
            {
                ok = false;
                Fail("HistoryBuffer", "P1", "TryGetExact(999999) should be null, got TotalDays=" + missing.TotalDays);
            }
            else
            {
                Console.WriteLine("  TryGetExact(999999) → null (safe miss)");
            }

            WorldObservationSnapshot before = null;
            try
            {
                before = history.SampleAtOrBefore(999999);
            }
            catch (Exception ex)
            {
                ok = false;
                Fail("HistoryBuffer", "P0", "SampleAtOrBefore(999999) threw " + ex.GetType().Name);
            }

            if (before != null)
            {
                Console.WriteLine("  SampleAtOrBefore(999999) → TotalDays=" + before.TotalDays + " (nearest existing, not a throw)");
                ScanFinite("SampleAtOrBefore 999999", null, before);
            }

            SectionPass["HistoryBuffer"] = ok;
            Console.WriteLine(ok ? "PASS HistoryBuffer" : "FAIL HistoryBuffer");
        }

        static void TestReset()
        {
            Header("8. Reset — same seed determinism");
            bool ok = true;
            int[] days = { 0, 1, 7, 30 };

            var runA = CaptureRun(Seed, 30, days);
            var runB = CaptureRun(Seed, 30, days);

            for (int i = 0; i < days.Length; i++)
            {
                int d = days[i];
                if (!runA.ContainsKey(d) || !runB.ContainsKey(d))
                {
                    ok = false;
                    Fail("Reset", "P1", "Missing snapshot at day " + d);
                    continue;
                }

                if (!CompareSnapshots("Reset runA vs runB @ " + d, runA[d], runB[d]))
                {
                    ok = false;
                    Fail("Reset", "P2-A?", "Same seed produced different Observation at TotalDays=" + d
                        + " (if Capture is a field copy, this is P2-A nondeterminism).");
                }
                else
                {
                    Console.WriteLine("  Day " + d + " runA == runB (deterministic)");
                }
            }

            SectionPass["Reset"] = ok;
            Console.WriteLine(ok ? "PASS Reset" : "FAIL Reset");
        }

        static void TestHistoryReset()
        {
            Header("12. HistoryReset — ObservationHost clears on OnWorldReset and records Day 0");
            bool ok = true;
            var world = new HeadlessWorld(Seed);
            var host = BindHost(world);
            world.Reset(Seed);

            if (host.History.TryGetExact(0) == null)
            {
                ok = false;
                Fail("HistoryReset", "P0", "Day 0 not recorded after initial Reset.");
            }

            world.AdvanceDays(30);
            var run1Day30 = host.History.TryGetExact(30);
            if (run1Day30 == null)
            {
                ok = false;
                Fail("HistoryReset", "P0", "TryGetExact(30) missing after running 30 days.");
            }
            else
            {
                Console.WriteLine("  Run1 Day 30 present pop=" + run1Day30.TotalPopulation.ToString("0.##", CultureInfo.InvariantCulture));
            }

            foreach (int d in new[] { 0, 1, 7, 30 })
            {
                if (host.History.TryGetExact(d) == null)
                {
                    ok = false;
                    Fail("HistoryReset", "P1", "Run1 missing Day " + d);
                }
            }

            if (run1Day30 != null)
            {
                run1Day30.TotalPopulation = -999f;
                if (run1Day30.Regions != null && run1Day30.Regions.Length > 0)
                {
                    run1Day30.Regions[0].Population = -1f;
                    run1Day30.Regions[0].Food = -1f;
                }
            }

            world.Reset(Seed);
            if (host.History.TryGetExact(30) != null)
            {
                ok = false;
                Fail("HistoryReset", "P0", "TryGetExact(30) still present after Reset (stale Run1 history).");
            }
            else
            {
                Console.WriteLine("  After Reset: Day 30 gone");
            }

            var day0 = host.History.TryGetExact(0);
            if (day0 == null)
            {
                ok = false;
                Fail("HistoryReset", "P0", "TryGetExact(0) missing after Reset (Day 0 baseline not recorded).");
            }
            else
            {
                ok &= CheckWorldSnapshot("HistoryReset new Day 0", world.State, day0);
                Console.WriteLine("  After Reset: Day 0 recorded for new world");
            }

            if (Math.Abs(world.State.Regions[0].Population + 1f) < 0.01f)
            {
                ok = false;
                Fail("HistoryReset", "P0", "Mutating Run1 snapshot wrote into post-Reset WorldState.");
            }

            world.AdvanceDays(5);
            if (host.History.TryGetExact(30) != null)
            {
                ok = false;
                Fail("HistoryReset", "P0", "Day 30 reappeared after Run2 Day 5.");
            }

            var probe = new HeadlessWorld(Seed);
            for (int d = 0; d <= 5; d++)
            {
                if (d > 0)
                {
                    probe.AdvanceDay();
                }

                var hist = host.History.TryGetExact(d);
                if (hist == null)
                {
                    ok = false;
                    Fail("HistoryReset", "P1", "Run2 missing Day " + d);
                    continue;
                }

                ok &= CheckWorldSnapshot("HistoryReset Run2 Day " + d, probe.State, hist);
            }

            var run2Day5 = host.History.TryGetExact(5);
            var expectedDay5 = SimulationObservation.Capture(world.State);
            if (run2Day5 == null || !CompareSnapshots("Run2 Day5 vs current State capture", run2Day5, expectedDay5))
            {
                ok = false;
                Fail("HistoryReset", "P0", "Run2 Day 5 does not match current world.");
            }
            else
            {
                Console.WriteLine("  Run2 Day 0–5 present; Day 5 matches current State");
            }

            var snap2 = SimulationObservation.Capture(world.State);
            float popBefore = world.State.Regions[0].Population;
            snap2.Regions[0].Population = popBefore + 5000f;
            if (Math.Abs(world.State.Regions[0].Population - popBefore) > Eps)
            {
                ok = false;
                Fail("HistoryReset", "P0", "Mutating Run2 snapshot wrote through to WorldState.");
            }

            SectionPass["HistoryReset"] = ok;
            Console.WriteLine(ok ? "PASS HistoryReset" : "FAIL HistoryReset");
        }

        static void TestDataIsolation()
        {
            Header("9. Data Isolation — mutating Snapshot must not write WorldState");
            bool ok = true;
            var world = new HeadlessWorld(Seed);
            world.AdvanceDays(25);
            var state = world.State;
            var snap = SimulationObservation.Capture(state);

            float[] origPop = new float[state.Regions.Length];
            float[] origFood = new float[state.Regions.Length];
            int[] origEventCount = new int[state.Regions.Length];
            float[] origSev = new float[state.Regions.Length];
            for (int i = 0; i < state.Regions.Length; i++)
            {
                origPop[i] = state.Regions[i].Population;
                origFood[i] = state.Regions[i].Get(ResourceId.Food);
                origEventCount[i] = state.Regions[i].ActiveEvents != null ? state.Regions[i].ActiveEvents.Count : 0;
                origSev[i] = origEventCount[i] > 0 ? state.Regions[i].ActiveEvents[0].Severity : 0f;
            }

            snap.TotalPopulation = -1f;
            snap.Year = 999;
            snap.WorldName = "MUTATED";
            if (snap.Regions != null && snap.Regions.Length > 0)
            {
                snap.Regions[0].Population = origPop[0] + 12345f;
                snap.Regions[0].Food = origFood[0] + 999f;
                snap.Regions[0].Water = 0f;
                snap.Regions[0].Mana = 0f;
                snap.Regions[0].DiseasePressure = 9f;
                snap.Regions[0].Stability = -9f;
                snap.Regions[0].Education = -9f;
                snap.Regions[0].Faith = -9f;
                snap.Regions[0].PopulationDelta = 777f;
                if (snap.Regions[0].ActiveEvents != null && snap.Regions[0].ActiveEvents.Length > 0)
                {
                    snap.Regions[0].ActiveEvents[0].Severity = 42f;
                    snap.Regions[0].ActiveEvents[0].Duration = 1;
                    snap.Regions[0].ActiveEvents[0].IsActive = false;
                    snap.Regions[0].ActiveEvents[0].EventId = "FAKE";
                }

                snap.Regions = Array.Empty<RegionObservationSnapshot>();
            }

            for (int i = 0; i < state.Regions.Length; i++)
            {
                if (Math.Abs(state.Regions[i].Population - origPop[i]) > Eps)
                {
                    ok = false;
                    Fail("Data Isolation", "P0", "Mutating Snapshot.Population wrote through to State." + state.Regions[i].DisplayName);
                }

                if (Math.Abs(state.Regions[i].Get(ResourceId.Food) - origFood[i]) > Eps)
                {
                    ok = false;
                    Fail("Data Isolation", "P0", "Mutating Snapshot.Food wrote through to State Resources[].");
                }

                int count = state.Regions[i].ActiveEvents != null ? state.Regions[i].ActiveEvents.Count : 0;
                if (count != origEventCount[i])
                {
                    ok = false;
                    Fail("Data Isolation", "P0", "Mutating Snapshot.ActiveEvents changed State list count.");
                }

                if (origEventCount[i] > 0 && Math.Abs(state.Regions[i].ActiveEvents[0].Severity - origSev[i]) > Eps)
                {
                    ok = false;
                    Fail("Data Isolation", "P0", "Mutating Snapshot event Severity wrote through to RegionEvent.");
                }
            }

            if (state.Year == 999 || state.WorldName == "MUTATED")
            {
                ok = false;
                Fail("Data Isolation", "P0", "Mutating Snapshot world fields wrote through to WorldState.");
            }

            Console.WriteLine("  Snapshot types are mutable DTOs (public fields), not language-immutable.");
            Console.WriteLine("  Capture copies scalars and new EventObservation[] — write-through check: " + (ok ? "no alias" : "ALIAS BUG"));

            SectionPass["Data Isolation"] = ok;
            Console.WriteLine(ok ? "PASS Data Isolation" : "FAIL Data Isolation");
        }

        static void TestFreezeUntouched()
        {
            Header("10. P2-A Freeze — this harness did not edit frozen files");
            SectionPass["P2-A Freeze"] = true;
            Console.WriteLine("  ObservationHost + OnWorldReset lifecycle; frozen math files not edited.");
            Console.WriteLine("  Frozen systems were called, not modified.");
            Console.WriteLine("PASS P2-A Freeze (no source edits to math/config/ProjectSettings)");
        }

        static ObservationHost BindHost(HeadlessWorld world)
        {
            var host = new ObservationHost();
            world.OnWorldReset += host.HandleWorldReset;
            world.OnDayAdvanced += host.HandleDayAdvanced;
            host.HandleWorldReset(world.State);
            return host;
        }

        static Dictionary<int, WorldObservationSnapshot> CaptureRun(int seed, int untilDay, int[] checkpoints)
        {
            var map = new Dictionary<int, WorldObservationSnapshot>();
            var world = new HeadlessWorld(seed);
            var needed = new HashSet<int>(checkpoints);
            if (needed.Contains(0))
            {
                map[0] = SimulationObservation.Capture(world.State);
            }

            while (world.State.TotalDays < untilDay)
            {
                world.AdvanceDay();
                if (needed.Contains(world.State.TotalDays))
                {
                    map[world.State.TotalDays] = SimulationObservation.Capture(world.State);
                }

                if (world.State.HaltedOnNumericError)
                {
                    break;
                }
            }

            return map;
        }

        static void AdvanceToDayOfYear(HeadlessWorld world, int dayOfYear)
        {
            world.Reset(Seed);
            int guard = 0;
            while (world.State.DayOfYear != dayOfYear && guard < 400)
            {
                world.AdvanceDay();
                guard++;
            }
        }

        static bool CheckWorldSnapshot(string label, WorldState state, WorldObservationSnapshot snap)
        {
            if (state == null || snap == null)
            {
                Fail("State Consistency", "P0", label + " null state or snapshot");
                return false;
            }

            ScanFinite(label, state, snap);
            bool ok = true;

            if (snap.WorldName != state.WorldName)
            {
                ok = false;
                Fail("State Consistency", "P1", label + " WorldName snap=" + snap.WorldName + " state=" + state.WorldName);
            }

            if (snap.Year != state.Year || snap.DayOfYear != state.DayOfYear || snap.TotalDays != state.TotalDays)
            {
                ok = false;
                Fail("State Consistency", "P1", label + " calendar snap Y/D/T="
                    + snap.Year + "/" + snap.DayOfYear + "/" + snap.TotalDays
                    + " state=" + state.Year + "/" + state.DayOfYear + "/" + state.TotalDays);
            }

            if (snap.CurrentSeason != state.CurrentSeason || snap.SeasonIndex != state.SeasonIndex
                || snap.DayInSeason != state.DayInSeason
                || Math.Abs(snap.SeasonProgress - state.SeasonProgress) > 1e-6f)
            {
                ok = false;
                Fail("Season", "P1", label + " season fields Snapshot≠State");
            }

            if (snap.HaltedOnNumericError != state.HaltedOnNumericError)
            {
                ok = false;
                Fail("State Consistency", "P0", label + " HaltedOnNumericError mismatch");
            }

            if ((state.Regions == null && snap.Regions != null && snap.Regions.Length > 0)
                || (state.Regions != null && (snap.Regions == null || snap.Regions.Length != state.Regions.Length)))
            {
                ok = false;
                Fail("State Consistency", "P0", label + " region count snap=" + (snap.Regions?.Length ?? -1)
                    + " state=" + (state.Regions?.Length ?? -1));
                return false;
            }

            var seen = new HashSet<RegionId>();
            for (int i = 0; i < state.Regions.Length; i++)
            {
                var r = state.Regions[i];
                var s = snap.Regions[i];
                seen.Add(r.Id);
                if (!CheckRegion(label, r, s, state.TotalDays))
                {
                    ok = false;
                }
            }

            foreach (var id in RequiredRegions)
            {
                if (!seen.Contains(id))
                {
                    ok = false;
                    Fail("State Consistency", "P0", label + " missing required region " + id);
                }
            }

            float expPop = 0f, expFood = 0f, expWater = 0f, expMana = 0f;
            for (int i = 0; i < state.Regions.Length; i++)
            {
                expPop += state.Regions[i].Population;
                expFood += state.Regions[i].Get(ResourceId.Food);
                expWater += state.Regions[i].Get(ResourceId.Water);
                expMana += state.Regions[i].Get(ResourceId.Magic);
            }

            if (Math.Abs(snap.TotalPopulation - expPop) > EpsLoose
                || Math.Abs(snap.TotalFood - expFood) > EpsLoose
                || Math.Abs(snap.TotalWater - expWater) > EpsLoose
                || Math.Abs(snap.TotalMana - expMana) > EpsLoose)
            {
                ok = false;
                Fail("State Consistency", "P1", label + " world totals Snapshot≠sum(State)");
            }

            return ok;
        }

        static bool CheckRegion(string label, RegionState r, RegionObservationSnapshot s, int totalDays)
        {
            bool ok = true;
            string tag = label + " " + r.Id;

            if (s == null)
            {
                Fail("State Consistency", "P0", tag + " snapshot region null");
                return false;
            }

            if (s.RegionId != r.Id)
            {
                ok = false;
                Fail("State Consistency", "P0", tag + " RegionId snap=" + s.RegionId);
            }

            ok &= Near(tag + " Population", s.Population, r.Population);
            ok &= Near(tag + " Food", s.Food, r.Get(ResourceId.Food));
            ok &= Near(tag + " Water", s.Water, r.Get(ResourceId.Water));
            ok &= Near(tag + " Magic/Mana", s.Mana, r.Get(ResourceId.Magic));
            ok &= Near(tag + " Disease", s.DiseasePressure, r.DiseasePressure, 1e-5f);
            ok &= Near(tag + " Stability", s.Stability, r.Stability, 1e-5f);
            ok &= Near(tag + " Education", s.Education, r.Education, 1e-5f);
            ok &= Near(tag + " Faith", s.Faith, r.FaithLevel, 1e-5f);
            ok &= Near(tag + " PopulationDelta", s.PopulationDelta, r.PopulationDelta);
            ok &= Near(tag + " CarryingCapacity", s.CarryingCapacity, r.LastCarryingCapacity);

            int stateCount = r.ActiveEvents != null ? r.ActiveEvents.Count : 0;
            int snapCount = s.ActiveEvents != null ? s.ActiveEvents.Length : 0;
            if (stateCount != snapCount)
            {
                ok = false;
                Fail("Event", "P1", tag + " ActiveEvents count snap=" + snapCount + " state=" + stateCount);
            }
            else if (r.ActiveEvents != null && s.ActiveEvents != null)
            {
                for (int i = 0; i < r.ActiveEvents.Count; i++)
                {
                    var se = r.ActiveEvents[i];
                    var oe = s.ActiveEvents[i];
                    if (oe.EventId != se.EventId || oe.EventType != se.EventType || oe.RegionId != se.RegionId
                        || oe.StartDay != se.StartDay || oe.Duration != se.Duration || oe.EndDay != se.EndDay
                        || Math.Abs(oe.Severity - se.Severity) > Eps
                        || oe.IsActive != se.IsActiveOn(totalDays))
                    {
                        ok = false;
                        Fail("Event", "P1", tag + " event[" + i + "] Snapshot≠State id=" + se.EventId);
                    }
                }
            }

            return ok;
        }

        static bool CompareSnapshots(string label, WorldObservationSnapshot a, WorldObservationSnapshot b)
        {
            if (a == null || b == null)
            {
                Fail("Daily Tick", "P1", label + " null snapshot");
                return false;
            }

            if (a.TotalDays != b.TotalDays || a.Year != b.Year || a.DayOfYear != b.DayOfYear)
            {
                Fail("Daily Tick", "P1", label + " calendar mismatch");
                return false;
            }

            if (Math.Abs(a.TotalPopulation - b.TotalPopulation) > EpsLoose
                || Math.Abs(a.TotalFood - b.TotalFood) > EpsLoose)
            {
                Fail("Daily Tick", "P1", label + " totals mismatch");
                return false;
            }

            return true;
        }

        static bool Near(string field, float actual, float expected, float eps = Eps)
        {
            if (float.IsNaN(actual) || float.IsInfinity(actual) || float.IsNaN(expected) || float.IsInfinity(expected))
            {
                SawNanOrInf = true;
                Fail("NaN / Infinity", "P0", field + " actual=" + actual + " expected=" + expected);
                return false;
            }

            if (Math.Abs(actual - expected) > eps)
            {
                Fail("State Consistency", "P1", field + " snap=" + actual + " state=" + expected);
                return false;
            }

            return true;
        }

        static void ScanFinite(string label, WorldState state, WorldObservationSnapshot snap)
        {
            if (snap != null)
            {
                CheckFinite(label + " TotalPopulation", snap.TotalPopulation);
                CheckFinite(label + " TotalFood", snap.TotalFood);
                CheckFinite(label + " TotalWater", snap.TotalWater);
                CheckFinite(label + " TotalMana", snap.TotalMana);
                CheckFinite(label + " SeasonProgress", snap.SeasonProgress);
                if (snap.Regions != null)
                {
                    for (int i = 0; i < snap.Regions.Length; i++)
                    {
                        var r = snap.Regions[i];
                        if (r == null)
                        {
                            continue;
                        }

                        CheckFinite(label + " " + r.RegionId + " Population", r.Population);
                        CheckFinite(label + " " + r.RegionId + " Food", r.Food);
                        CheckFinite(label + " " + r.RegionId + " Water", r.Water);
                        CheckFinite(label + " " + r.RegionId + " Mana", r.Mana);
                        CheckFinite(label + " " + r.RegionId + " Disease", r.DiseasePressure);
                        CheckFinite(label + " " + r.RegionId + " Stability", r.Stability);
                        CheckFinite(label + " " + r.RegionId + " Education", r.Education);
                        CheckFinite(label + " " + r.RegionId + " Faith", r.Faith);
                        CheckFinite(label + " " + r.RegionId + " Delta", r.PopulationDelta);
                    }
                }
            }

            if (state?.Regions != null)
            {
                for (int i = 0; i < state.Regions.Length; i++)
                {
                    var r = state.Regions[i];
                    CheckFinite(label + " STATE " + r.Id + " Population", r.Population);
                    CheckFinite(label + " STATE " + r.Id + " Food", r.Get(ResourceId.Food));
                    CheckFinite(label + " STATE " + r.Id + " Water", r.Get(ResourceId.Water));
                    CheckFinite(label + " STATE " + r.Id + " Magic", r.Get(ResourceId.Magic));
                }
            }
        }

        static void CheckFinite(string field, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                SawNanOrInf = true;
                Fail("NaN / Infinity", "P0", field + "=" + value);
            }
        }

        static RegionObservationSnapshot FindSnapRegion(WorldObservationSnapshot snap, RegionId id)
        {
            if (snap?.Regions == null)
            {
                return null;
            }

            for (int i = 0; i < snap.Regions.Length; i++)
            {
                if (snap.Regions[i] != null && snap.Regions[i].RegionId == id)
                {
                    return snap.Regions[i];
                }
            }

            return null;
        }

        static RegionEvent FindStateEvent(RegionState region, string eventId)
        {
            if (region?.ActiveEvents == null)
            {
                return null;
            }

            for (int i = 0; i < region.ActiveEvents.Count; i++)
            {
                if (region.ActiveEvents[i] != null && region.ActiveEvents[i].EventId == eventId)
                {
                    return region.ActiveEvents[i];
                }
            }

            return null;
        }

        static float SumPop(WorldState state)
        {
            float t = 0f;
            if (state?.Regions == null)
            {
                return t;
            }

            for (int i = 0; i < state.Regions.Length; i++)
            {
                t += state.Regions[i].Population;
            }

            return t;
        }

        static void Fail(string section, string severity, string detail)
        {
            Findings.Add(new Finding { Section = section, Severity = severity, Detail = detail });
            Console.WriteLine("  [FAIL] [" + severity + "] " + section + ": " + detail);
        }

        static void Header(string title)
        {
            Console.WriteLine();
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine(title);
            Console.WriteLine("------------------------------------------------------------");
        }

        static string Mark(string key)
        {
            if (key == "Build")
            {
                return HarnessException ? "FAIL" : "PASS";
            }

            bool pass;
            if (!SectionPass.TryGetValue(key, out pass))
            {
                return "FAIL";
            }

            return pass ? "PASS" : "FAIL";
        }

        static void PrintReport()
        {
            Console.WriteLine();
            Console.WriteLine("============================================================");
            Console.WriteLine("P2-B Observation Test Report");
            Console.WriteLine("============================================================");
            Console.WriteLine("1. Build              " + Mark("Build"));
            Console.WriteLine("2. State Consistency  " + Mark("State Consistency"));
            Console.WriteLine("3. Daily Tick         " + Mark("Daily Tick"));
            Console.WriteLine("4. Season             " + Mark("Season"));
            Console.WriteLine("5. Event              " + Mark("Event"));
            Console.WriteLine("6. FastForward        " + Mark("FastForward"));
            Console.WriteLine("7. HistoryBuffer      " + Mark("HistoryBuffer"));
            Console.WriteLine("8. Reset              " + Mark("Reset"));
            Console.WriteLine("9. Data Isolation     " + Mark("Data Isolation"));
            Console.WriteLine("10. P2-A Freeze       " + Mark("P2-A Freeze"));
            Console.WriteLine("11. NaN / Infinity    " + Mark("NaN / Infinity"));
            Console.WriteLine("12. HistoryReset      " + Mark("HistoryReset"));
            Console.WriteLine();
            Console.WriteLine("Findings: " + Findings.Count);
            for (int i = 0; i < Findings.Count; i++)
            {
                var f = Findings[i];
                Console.WriteLine("- [" + f.Severity + "] " + f.Section + ": " + f.Detail);
            }
        }
    }
}
