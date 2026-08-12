using System;
using System.Collections.Generic;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Observation;

namespace HeadlessSimTests
{
    /// <summary>
    /// P2-B observation-layer smoke tests: Capture must mirror Simulation State, not recompute formulas.
    /// </summary>
    public static class ObservationTests
    {
        public static int Run()
        {
            var failures = new List<string>();
            Run("Capture_MatchesWorldStateFields", Capture_MatchesWorldStateFields, failures);
            Run("History_RecordsAndSamplesByTotalDays", History_RecordsAndSamplesByTotalDays, failures);
            Run("Capture_AfterAdvanceDay_TracksPopulationFromState", Capture_AfterAdvanceDay_TracksPopulationFromState, failures);

            Console.WriteLine();
            Console.WriteLine($"Observation Result: {3 - failures.Count}/3 passed");
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
    }
}
