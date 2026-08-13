using System;
using System.Collections.Generic;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;

namespace HeadlessSimTests
{
    /// <summary>
    /// P2-B observation-layer tests. Capture must mirror Simulation State, not recompute formulas.
    /// </summary>
    public static class ObservationTests
    {
        public static int Run()
        {
            var failures = new List<string>();
            Run("Capture_MatchesWorldStateFields", Capture_MatchesWorldStateFields, failures);
            Run("History_RecordsAndSamplesByTotalDays", History_RecordsAndSamplesByTotalDays, failures);
            Run("Capture_AfterAdvanceDay_TracksPopulationFromState", Capture_AfterAdvanceDay_TracksPopulationFromState, failures);
            Run("V02_ThreeRegionsDisplayedFromObservation", V02_ThreeRegionsDisplayedFromObservation, failures);
            Run("V02_RegionSwitchSelectsCorrectSnapshot", V02_RegionSwitchSelectsCorrectSnapshot, failures);
            Run("V02_ResourcesMatchObservation", V02_ResourcesMatchObservation, failures);
            Run("V02_SeasonCopiedNotRecomputed", V02_SeasonCopiedNotRecomputed, failures);
            Run("V02_EventsStayOnOwningRegion", V02_EventsStayOnOwningRegion, failures);
            Run("V02_ResetShowsNewWorld", V02_ResetShowsNewWorld, failures);
            Run("V02_FastForwardShowsNewState", V02_FastForwardShowsNewState, failures);
            Run("V02_UiVersion", V02_UiVersion, failures);
            Run("V02_NoNaNOrInfinity", V02_NoNaNOrInfinity, failures);
            Run("V02_PopulationVisualizerReadsSnapshotOnly", V02_PopulationVisualizerReadsSnapshotOnly, failures);
            Run("V02_P2AFreeze_CaptureStillMirrorsState", V02_P2AFreeze_CaptureStillMirrorsState, failures);

            int total = 14;
            Console.WriteLine();
            Console.WriteLine($"Observation Result: {total - failures.Count}/{total} passed");
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

        static void Capture_MatchesWorldStateFields()
        {
            var world = new HeadlessWorld(20260810);
            var state = world.State;
            var snap = SimulationObservation.Capture(state);

            AssertTrue(snap.Year == state.Year, "Year");
            AssertTrue(snap.DayOfYear == state.DayOfYear, "DayOfYear");
            AssertTrue(snap.TotalDays == state.TotalDays, "TotalDays");
            AssertTrue(snap.CurrentSeason == state.CurrentSeason, "CurrentSeason");
            AssertTrue(snap.SeasonIndex == state.SeasonIndex, "SeasonIndex");
            AssertTrue(snap.DayInSeason == state.DayInSeason, "DayInSeason");
            AssertTrue(Math.Abs(snap.SeasonProgress - state.SeasonProgress) < 1e-6f, "SeasonProgress");
            AssertTrue(snap.DaysPerYear == SimulationConfig.DaysPerYear, "DaysPerYear copied");
            AssertTrue(snap.Regions.Length == state.Regions.Length, "region count");

            float expectedPop = 0f;
            for (int i = 0; i < state.Regions.Length; i++)
            {
                var r = state.Regions[i];
                var s = snap.Regions[i];
                AssertTrue(s.RegionId == r.Id, "RegionId");
                AssertTrue(Math.Abs(s.Population - r.Population) < 1e-4f, "Population " + r.DisplayName);
                AssertTrue(Math.Abs(s.Food - r.Get(ResourceId.Food)) < 1e-4f, "Food");
                AssertTrue(Math.Abs(s.Water - r.Get(ResourceId.Water)) < 1e-4f, "Water");
                AssertTrue(Math.Abs(s.Timber - r.Get(ResourceId.Timber)) < 1e-4f, "Timber");
                AssertTrue(Math.Abs(s.Ore - r.Get(ResourceId.Ore)) < 1e-4f, "Ore");
                AssertTrue(Math.Abs(s.Mana - r.Get(ResourceId.Magic)) < 1e-4f, "Mana/Magic");
                AssertTrue(Math.Abs(s.DiseasePressure - r.DiseasePressure) < 1e-6f, "Disease");
                AssertTrue(Math.Abs(s.Stability - r.Stability) < 1e-6f, "Stability");
                AssertTrue(Math.Abs(s.Education - r.Education) < 1e-6f, "Education");
                AssertTrue(Math.Abs(s.Faith - r.FaithLevel) < 1e-6f, "Faith");
                expectedPop += r.Population;
            }

            AssertTrue(Math.Abs(snap.TotalPopulation - expectedPop) < 1e-3f, "TotalPopulation sum");
        }

        static void History_RecordsAndSamplesByTotalDays()
        {
            var world = new HeadlessWorld(20260810);
            var history = new SimulationHistoryBuffer(128);
            history.Record(SimulationObservation.Capture(world.State));

            world.AdvanceDays(30);
            history.Record(SimulationObservation.Capture(world.State));

            var day0 = history.TryGetExact(0);
            AssertTrue(day0 != null, "day0 sample");
            AssertTrue(day0.TotalDays == 0, "day0 TotalDays");

            var day30 = history.TryGetExact(30);
            AssertTrue(day30 != null, "day30 sample");
            AssertTrue(day30.TotalDays == 30, "day30 TotalDays");

            var before = history.SampleAtOrBefore(15);
            AssertTrue(before != null && before.TotalDays == 0, "at-or-before 15 → day0");

            var exactish = history.SampleAtOrBefore(30);
            AssertTrue(exactish != null && exactish.TotalDays == 30, "at-or-before 30 → day30");
        }

        static void Capture_AfterAdvanceDay_TracksPopulationFromState()
        {
            var world = new HeadlessWorld(20260810);
            world.AdvanceDays(10);
            var afterSnap = SimulationObservation.Capture(world.State);
            float afterFromState = 0f;
            foreach (var r in world.State.Regions)
            {
                afterFromState += r.Population;
            }

            AssertTrue(Math.Abs(afterSnap.TotalPopulation - afterFromState) < 1e-3f,
                "snapshot population must equal summed State after ticks");
            AssertTrue(afterSnap.TotalDays == world.State.TotalDays, "TotalDays must match State");
        }

        static void V02_ThreeRegionsDisplayedFromObservation()
        {
            var hub = HubFromFreshWorld();
            AssertTrue(hub.Current.Regions.Length == 3, "three regions");
            AssertTrue(hub.FindRegion(RegionId.Theocracy) != null, "教廷区");
            AssertTrue(hub.FindRegion(RegionId.Empire) != null, "帝国区");
            AssertTrue(hub.FindRegion(RegionId.Sea) != null, "海");

            var text = ObservationPanelText.FormatRegionPanel(hub.Current, hub.FindRegion(RegionId.Theocracy));
            AssertTrue(text.Contains("教廷区"), "panel name");
            AssertTrue(text.Contains("人口"), "panel population label");
            AssertTrue(text.Contains("粮食"), "panel food");
            AssertTrue(text.Contains("水"), "panel water");
            AssertTrue(text.Contains("木"), "panel timber");
            AssertTrue(text.Contains("矿"), "panel ore");
            AssertTrue(text.Contains("魔力"), "panel mana");
        }

        static void V02_RegionSwitchSelectsCorrectSnapshot()
        {
            var hub = HubFromFreshWorld();
            hub.SelectRegion(RegionId.Empire);
            AssertTrue(hub.SelectedRegionId == RegionId.Empire, "selected id");
            AssertTrue(hub.SelectedRegion.RegionId == RegionId.Empire, "selected snapshot");
            AssertTrue(hub.SelectedRegion.DisplayName == hub.FindRegion(RegionId.Empire).DisplayName, "name");

            hub.SelectRegion(RegionId.Sea);
            AssertTrue(hub.SelectedRegion.RegionId == RegionId.Sea, "switch to sea");
            AssertTrue(hub.SelectedRegion.RegionId != RegionId.Empire, "not empire after switch");
        }

        static void V02_ResourcesMatchObservation()
        {
            var world = new HeadlessWorld(20260810);
            world.AdvanceDays(5);
            var hub = new ObservationHost();
            hub.RecordCurrent(world.State);
            foreach (var r in world.State.Regions)
            {
                var s = hub.FindRegion(r.Id);
                AssertTrue(Math.Abs(s.Population - r.Population) < 1e-4f, "pop " + r.Id);
                AssertTrue(Math.Abs(s.Food - r.Get(ResourceId.Food)) < 1e-4f, "food");
                AssertTrue(Math.Abs(s.Water - r.Get(ResourceId.Water)) < 1e-4f, "water");
                AssertTrue(Math.Abs(s.Mana - r.Get(ResourceId.Magic)) < 1e-4f, "mana");
                AssertTrue(Math.Abs(s.Timber - r.Get(ResourceId.Timber)) < 1e-4f, "timber");
                AssertTrue(Math.Abs(s.Ore - r.Get(ResourceId.Ore)) < 1e-4f, "ore");
            }
        }

        static void V02_SeasonCopiedNotRecomputed()
        {
            var world = new HeadlessWorld(20260810);
            world.AdvanceDays(100);
            var hub = new ObservationHost();
            hub.RecordCurrent(world.State);
            AssertTrue(hub.Current.CurrentSeason == world.State.CurrentSeason, "season enum from state");
            AssertTrue(hub.Current.DayInSeason == world.State.DayInSeason, "day in season from state");
            AssertTrue(ObservationLabels.SeasonName(hub.Current.CurrentSeason) == "Summer", "Summer after day 100");
            var header = ObservationPanelText.FormatWorldHeader(hub.Current);
            AssertTrue(header.Contains("Season Summer"), header);
            AssertTrue(header.Contains($"季内第 {hub.Current.DayInSeason} / {hub.Current.DaysPerSeason} 日"), "in-season day");
        }

        static void V02_EventsStayOnOwningRegion()
        {
            var world = new HeadlessWorld(20260810);
            world.Region(RegionId.Empire).ActiveEvents.Add(new RegionEvent
            {
                EventId = "test-shortage",
                EventType = SimEventType.FoodShortage,
                RegionId = RegionId.Empire,
                StartDay = 0,
                Duration = 12,
                Severity = 1.25f
            });

            var hub = new ObservationHost();
            hub.RecordCurrent(world.State);

            var empire = hub.FindRegion(RegionId.Empire);
            var theo = hub.FindRegion(RegionId.Theocracy);
            var sea = hub.FindRegion(RegionId.Sea);

            AssertTrue(ObservationPanelText.RegionHasActiveEvent(empire), "empire has event");
            AssertTrue(!ObservationPanelText.RegionHasActiveEvent(theo), "theocracy must not inherit empire event");
            AssertTrue(!ObservationPanelText.RegionHasActiveEvent(sea), "sea must not inherit empire event");

            var ev = ObservationPanelText.DominantActiveEvent(empire);
            AssertTrue(ev != null && ev.RegionId == RegionId.Empire, "event region id");
            AssertTrue(ev.DisplayName == ObservationLabels.EventDisplayName(SimEventType.FoodShortage), "name from observation");
            AssertTrue(ev.RemainingDays == 12, "remaining copied at capture");

            string empireText = ObservationPanelText.FormatEvents(empire);
            string theoText = ObservationPanelText.FormatEvents(theo);
            AssertTrue(empireText.Contains("粮食短缺"), empireText);
            AssertTrue(theoText == ObservationLabels.NoEvent, theoText);
        }

        static void V02_ResetShowsNewWorld()
        {
            var world = new HeadlessWorld(20260810);
            var hub = new ObservationHost();
            hub.RecordCurrent(world.State);
            float day0Pop = hub.Current.TotalPopulation;

            world.AdvanceDays(40);
            hub.RecordCurrent(world.State);
            AssertTrue(hub.Current.TotalDays == 40, "advanced");

            world.Reset(20260810);
            hub.HandleWorldReset(world.State);
            AssertTrue(hub.Current.TotalDays == 0, "reset TotalDays");
            AssertTrue(hub.Current.Year == 1 && hub.Current.DayOfYear == 1, "reset calendar");
            AssertTrue(Math.Abs(hub.Current.TotalPopulation - day0Pop) < 1e-3f, "reset population back to factory");
        }

        static void V02_FastForwardShowsNewState()
        {
            var world = new HeadlessWorld(20260810);
            var hub = new ObservationHost();
            hub.RecordCurrent(world.State);
            int before = hub.Current.TotalDays;

            var result = world.FastForwardDays(360);
            hub.RecordCurrent(result.State);
            AssertTrue(hub.Current.TotalDays == result.State.TotalDays, "FF TotalDays");
            AssertTrue(hub.Current.TotalDays == before + 360, "jumped 360");
            AssertTrue(hub.Current.Year == result.State.Year, "FF year");
            float sum = 0f;
            foreach (var r in result.State.Regions)
            {
                sum += r.Population;
            }

            AssertTrue(Math.Abs(hub.Current.TotalPopulation - sum) < 1e-2f, "FF population from new state");
        }

        static void V02_UiVersion()
        {
            AssertTrue(ObservationLabels.UiVersion == "P2-B · Observation v0.2", ObservationLabels.UiVersion);
            var header = ObservationPanelText.FormatWorldHeader(HubFromFreshWorld().Current);
            AssertTrue(header.StartsWith("P2-B · Observation v0.2", StringComparison.Ordinal), header);
        }

        static void V02_NoNaNOrInfinity()
        {
            var world = new HeadlessWorld(20260810);
            world.AdvanceDays(360);
            var hub = new ObservationHost();
            hub.RecordCurrent(world.State);
            AssertTrue(ObservationPanelText.SnapshotIsFinite(hub.Current), "finite after 360d");
            AssertTrue(!world.State.HaltedOnNumericError, "sim not halted");
        }

        static void V02_PopulationVisualizerReadsSnapshotOnly()
        {
            var hub = HubFromFreshWorld();
            var viz = new PendingRegionPopulationVisualizer();
            var region = hub.FindRegion(RegionId.Empire);
            viz.Apply(region);
            AssertTrue(Math.Abs(viz.LastObservedPopulation - region.Population) < 1e-4f, "visualizer reads snapshot.Population");
        }

        static void V02_P2AFreeze_CaptureStillMirrorsState()
        {
            var world = new HeadlessWorld(20260810);
            world.AdvanceDays(30);
            var snap = SimulationObservation.Capture(world.State);
            for (int i = 0; i < world.State.Regions.Length; i++)
            {
                var r = world.State.Regions[i];
                var s = snap.Regions[i];
                AssertTrue(Math.Abs(s.Population - r.Population) < 1e-4f, "freeze: pop still from state");
                AssertTrue(Math.Abs(s.Food - r.Get(ResourceId.Food)) < 1e-4f, "freeze: food still from state");
            }
        }

        static ObservationHost HubFromFreshWorld()
        {
            var world = new HeadlessWorld(20260810);
            var hub = new ObservationHost();
            hub.RecordCurrent(world.State);
            return hub;
        }
    }
}
