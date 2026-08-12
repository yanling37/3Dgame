using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using DivineWorld.Simulation.Systems;
using DivineWorld.Simulation.Testing;
using UnityEngine;

namespace HeadlessSimTests
{
    /// <summary>
    /// P2-A2 math acceptance diagnostic harness.
    /// Observes only — does not modify simulation formulas or balance parameters.
    /// Each test independently Reset(seed) from identical Day-0 state.
    /// </summary>
    public static class Phase2A2Tests
    {
        public const int Seed = 20260810;
        static readonly int[] CheckpointDays = { 1, 30, 90, 180, 270, 360 };
        static readonly List<Finding> Findings = new List<Finding>();

        public static int Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Findings.Clear();

            Header("P2-A2 Population / Water / Food / CarryingCapacity Math Diagnostic");
            Console.WriteLine($"Seed={Seed}");
            Console.WriteLine("Policy: no formula changes, no threshold tuning, no balance edits.");
            Console.WriteLine("Each test independently Reset from identical Day-0 state.");
            Console.WriteLine();

            TestA_PopulationBaseline();
            TestB_FertilitySingleVariable();
            TestC_CarryingCapacityFeedback();
            TestD_WaterToCarryingCapacity();
            TestE_WaterToFood();
            TestF_DeathDecomposition();
            TestG_DiseaseSingleVariable();
            TestH_LongTermNumericStability();
            TestI_EventIndependence();
            TestJ_DisasterDuration();
            TestFastForward_360();
            TestFastForward_Long();

            PrintFindingsSummary();
            return 0; // diagnostic always returns 0; findings are classified, not gated
        }

        // ================================================================
        // TEST A — Population birth/death baseline
        // ================================================================
        static void TestA_PopulationBaseline()
        {
            Section("TEST A — Population Birth/Death Baseline");
            Console.WriteLine("Params: Fertility×1.00 Harvest×1.00 Disease×1.00 Stability×1.00");
            Console.WriteLine($"Checkpoints: {string.Join(", ", CheckpointDays)}");
            Console.WriteLine();

            var world = NewWorld(1f, 1f, 1f, 1f);
            var day0Pops = CapturePops(world);
            PrintDay0Basics(world);

            int nextCp = 0;
            var cum = new CumMap();
            bool extend720 = false;

            for (int day = 1; day <= 360; day++)
            {
                AdvanceAccumulate(world, cum);
                if (nextCp < CheckpointDays.Length && day == CheckpointDays[nextCp])
                {
                    PrintCheckpoint("A", world, cum, day0Pops, day);
                    nextCp++;
                }

                if (world.State.HaltedOnNumericError)
                {
                    Record("A", "P0", true, false,
                        $"Halted at day {day}: {world.State.LastNumericError}");
                    break;
                }
            }

            // Heuristic: if any region declined >15% or Birth/Death < 0.85, extend to D720.
            foreach (var r in world.State.Regions)
            {
                float d0 = day0Pops[r.Id];
                if (d0 > 0f && r.Population < d0 * 0.85f)
                {
                    extend720 = true;
                }

                var c = cum.For(r.Id);
                if (c.TotalDeaths > 1f && c.Births / c.TotalDeaths < 0.85f)
                {
                    extend720 = true;
                }
            }

            if (extend720 && !world.State.HaltedOnNumericError)
            {
                Console.WriteLine("--- Extension D720 (anomaly: decline or Birth/Death < 0.85) ---");
                for (int day = 361; day <= 720; day++)
                {
                    AdvanceAccumulate(world, cum);
                    if (day == 720)
                    {
                        PrintCheckpoint("A", world, cum, day0Pops, day);
                    }
                }
            }

            foreach (var r in world.State.Regions)
            {
                var c = cum.For(r.Id);
                float ratio = c.TotalDeaths > 1e-6f ? c.Births / c.TotalDeaths : float.PositiveInfinity;
                float d0 = day0Pops[r.Id];
                string trend = r.Population >= d0 ? "growth/flat" : "decline";
                Console.WriteLine($"A/{r.DisplayName}: {trend}  Birth/Death={F4(ratio)}  " +
                                  $"CumBirths={F(c.Births)} CumDeaths={F(c.TotalDeaths)}  " +
                                  $"Pop {F(d0)} → {F(r.Population)}");
                if (ratio < 1f)
                {
                    Record("A", "P2", false, false,
                        $"{r.DisplayName}: under normal modifiers Birth/Death={F4(ratio)} < 1 → net decline. " +
                        $"Births={F(c.Births)} Deaths={F(c.TotalDeaths)} " +
                        $"(Natural={F(c.NaturalDeaths)} Disease={F(c.DiseaseDeaths)} OverCap={F(c.OverCapacityDeaths)}). " +
                        "Not a wiring bug by itself — balance / pressure observation.");
                }
            }

            Console.WriteLine();
        }

        // ================================================================
        // TEST B — Fertility single variable
        // ================================================================
        static void TestB_FertilitySingleVariable()
        {
            Section("TEST B — Fertility Single Variable (×0.70 / ×1.00 / ×1.30)");
            Console.WriteLine("Fixed: Harvest×1.00 Disease×1.00 Stability×1.00  Days=360");
            Console.WriteLine();

            float[] ferts = { 0.70f, 1.00f, 1.30f };
            string[] labels = { "A", "B", "C" };
            var arms = new List<(string Label, float Fert, CumMap Cum, HeadlessWorld World, Dictionary<RegionId, DayMetrics> Last)>();

            for (int i = 0; i < ferts.Length; i++)
            {
                var world = NewWorld(ferts[i], 1f, 1f, 1f);
                var cum = new CumMap();
                for (int d = 0; d < 360; d++)
                {
                    AdvanceAccumulate(world, cum);
                }

                var last = CaptureAllMetrics(world);
                arms.Add((labels[i], ferts[i], cum, world, last));
            }

            foreach (RegionId id in Enum.GetValues(typeof(RegionId)))
            {
                Console.WriteLine($"--- Region {id} ---");
                for (int i = 0; i < arms.Count; i++)
                {
                    var a = arms[i];
                    var m = a.Last[id];
                    var c = a.Cum.For(id);
                    Console.WriteLine(
                        $"  {a.Label} Fert×{a.Fert:0.00}: EffBirthRate={F6(m.EffectiveBirthRate)}  " +
                        $"CumBirths={F(c.Births)} CumDeaths={F(c.TotalDeaths)}  " +
                        $"FinalPop={F(m.Population)} Net={F(c.NetChange)}  " +
                        $"ΔBirths vs B={(i == 1 ? 0f : c.Births - arms[1].Cum.For(id).Births):+0.##}  " +
                        $"ΔDeaths vs B={(i == 1 ? 0f : c.TotalDeaths - arms[1].Cum.For(id).TotalDeaths):+0.##}");
                }

                float eA = arms[0].Last[id].EffectiveBirthRate;
                float eB = arms[1].Last[id].EffectiveBirthRate;
                float eC = arms[2].Last[id].EffectiveBirthRate;
                float bA = arms[0].Cum.For(id).Births;
                float bB = arms[1].Cum.For(id).Births;
                float bC = arms[2].Cum.For(id).Births;

                bool rateOk = eA < eB && eB < eC;
                bool birthOk = bA < bB && bB < bC;
                Console.WriteLine($"  Check EffectiveBirthRate A<B<C: {(rateOk ? "YES" : "NO")} ({F6(eA)} < {F6(eB)} < {F6(eC)})");
                Console.WriteLine($"  Check CumBirths A<B<C: {(birthOk ? "YES" : "NO")} ({F(bA)} < {F(bB)} < {F(bC)})");

                if (!rateOk || !birthOk)
                {
                    Record("B", "P1", true, true,
                        $"{id}: Fertility monotonicity broken. rateOk={rateOk} birthOk={birthOk}");
                }
                else
                {
                    Console.WriteLine("  Note: final population may still decline in all arms — that is not a Fertility wiring failure.");
                }
            }

            Console.WriteLine();
        }

        // ================================================================
        // TEST C — Carrying capacity feedback
        // ================================================================
        static void TestC_CarryingCapacityFeedback()
        {
            Section("TEST C — Carrying Capacity Feedback (C1 Pop<K / C2 Pop≈K / C3 Pop>K)");
            Console.WriteLine("Constructed instantaneous states; formulas not modified. OverCapacityDeaths mirrored from PopulationSystem.");
            Console.WriteLine();

            var baseWorld = NewWorld(1f, 1f, 1f, 1f);
            // Warm one day so LastWaterFactor is set from ResourceSystem.
            baseWorld.AdvanceDay();

            foreach (var regionTemplate in baseWorld.State.Regions)
            {
                var race = FindRace(baseWorld.Races, regionTemplate.DominantRace);
                float k0 = PopulationSystem.CalculateCarryingCapacity(
                    regionTemplate, race, baseWorld.State.CurrentSeason, baseWorld.Config);

                var cases = new (string Name, float PopMul)[]
                {
                    ("C1 Pop<K", 0.50f),
                    ("C2 Pop≈K", 1.00f),
                    ("C3 Pop>K", 1.40f)
                };

                Console.WriteLine($"--- {regionTemplate.DisplayName} (ref K≈{F(k0)}) ---");
                foreach (var c in cases)
                {
                    var region = regionTemplate.Clone();
                    region.Population = Mathf.Max(1f, k0 * c.PopMul);
                    // Keep food/water stocks scaled so foodRatio / water don't dominate the logistic signal.
                    region.Set(ResourceId.Food, region.Population * baseWorld.Config.FoodRatioSoftCap);
                    region.Set(ResourceId.Water, Mathf.Max(region.Get(ResourceId.Water),
                        region.Population * baseWorld.Config.WaterAvailabilityNormPerCapita));

                    var m = MirrorPopulation(region, race, baseWorld.State.CurrentSeason, baseWorld.Config);
                    Console.WriteLine(
                        $"  {c.Name}: Pop={F(m.Population)} K={F(m.CarryingCapacity)} Pop/K={F4(m.PopOverK)}  " +
                        $"LogisticBirthFactor={F4(m.LogisticBirthFactor)} EffBirthRate={F6(m.EffectiveBirthRate)}  " +
                        $"Births={F4(m.Births)} Deaths={F4(m.TotalDeaths)} Net={F4(m.NetChange)}  " +
                        $"OverCapacityDeaths={F4(m.OverCapacityDeaths)}");

                    if (c.Name.StartsWith("C1") && m.LogisticBirthFactor <= 0f)
                    {
                        Record("C", "P1", true, true, $"{region.DisplayName} C1: LogisticBirthFactor <= 0 while Pop<K");
                    }

                    if (c.Name.StartsWith("C2") && Math.Abs(m.LogisticBirthFactor) > 0.08f)
                    {
                        Record("C", "P2", false, false,
                            $"{region.DisplayName} C2: LogisticBirthFactor={F4(m.LogisticBirthFactor)} not near 0 at Pop≈K " +
                            "(expected ~0; small drift OK if K recalculates with Pop).");
                    }

                    if (c.Name.StartsWith("C3"))
                    {
                        if (m.Births > 1e-3f && m.LogisticBirthFactor > 1e-4f)
                        {
                            Record("C", "P1", true, true,
                                $"{region.DisplayName} C3: Births not suppressed when Pop>K (Births={F4(m.Births)})");
                        }

                        // OverCapacityDeaths may be >0 under current formula — record observation, not force-zero.
                        if (m.OverCapacityDeaths > 0f)
                        {
                            Record("C", "P2", false, false,
                                $"{region.DisplayName} C3: OverCapacityDeaths={F4(m.OverCapacityDeaths)} > 0 " +
                                $"(formula applies OverpopulationDeathRate when Pop>K; folded into LastNaturalDeath). " +
                                "Prior P2-A1 note that OverCapacityDeaths are always 0 is outdated.");
                        }
                    }

                    // Clamp check: population value itself is never forced to K by CalculateCarryingCapacity.
                    if (Math.Abs(region.Population - m.CarryingCapacity) < 1e-3f && c.Name.StartsWith("C3"))
                    {
                        Record("C", "P1", true, true, $"{region.DisplayName}: Population appears clamped to K");
                    }
                }
            }

            Console.WriteLine();
        }

        // ================================================================
        // TEST D — Water → CarryingCapacity
        // ================================================================
        static void TestD_WaterToCarryingCapacity()
        {
            Section("TEST D — Water → CarryingCapacity (focus)");
            Console.WriteLine("Fixed Population/Land/Education; vary WaterStock as % of waterNeed (=Pop×WaterAvailabilityNormPerCapita).");
            Console.WriteLine("K waterFactor = Clamp(WaterStock/waterNeed, 0.05, 2.0) — floor is 0.05, not 0.1.");
            Console.WriteLine();

            float[] levels = { 1.00f, 0.75f, 0.50f, 0.25f, 0.10f, 0.00f };
            var world = NewWorld(1f, 1f, 1f, 1f);

            foreach (var regionTemplate in world.State.Regions)
            {
                var race = FindRace(world.Races, regionTemplate.DominantRace);
                Console.WriteLine($"--- {regionTemplate.DisplayName} (Pop fixed={F(regionTemplate.Population)}) ---");

                float prevK = float.NaN;
                bool monotonic = true;
                bool lockedAtFloor = false;

                foreach (float lvl in levels)
                {
                    var region = regionTemplate.Clone();
                    float waterNeed = Mathf.Max(1f, region.Population * world.Config.WaterAvailabilityNormPerCapita);
                    float waterStock = waterNeed * lvl;
                    region.Set(ResourceId.Water, waterStock);

                    // Resolve agri water factor so LastWaterFactor matches resource path used inside K foodFactor.
                    float labor = ResourceSystem.ComputeLabor(region, world.Config);
                    float tech = world.Config.TechBase + region.Education * world.Config.TechFromEducation;
                    float unconstrained = ResourceSystem.CalculateUnconstrainedFoodProduction(
                        region, race, SeasonId.Spring, world.Config, labor, tech, 1f, 1f);
                    ResourceSystem.ResolveWaterAllocation(region, unconstrained, world.Config,
                        out float agriWaterFactor, out _, out _, out float dailyWaterCons);

                    float k = PopulationSystem.CalculateCarryingCapacity(region, race, SeasonId.Spring, world.Config);
                    float kWaterFactor = Mathf.Clamp(waterStock / waterNeed, 0.05f, 2f);
                    float waterCap = ResourceSystem.GetWaterCapacity(region, SeasonId.Spring, world.Config);

                    Console.WriteLine(
                        $"  WaterAvail={lvl * 100f:0}%  WaterStock={F(waterStock)} WaterCap={F(waterCap)}  " +
                        $"DailyWaterCons≈{F4(dailyWaterCons)}  " +
                        $"K.WaterFactor={F4(kWaterFactor)} Agri.WaterFactor={F4(agriWaterFactor)}  " +
                        $"K={F(k)} Pop={F(region.Population)}");

                    if (!float.IsNaN(prevK) && k > prevK + 1e-2f)
                    {
                        monotonic = false;
                    }

                    if (lvl <= 0.10f && Math.Abs(kWaterFactor - 0.05f) < 1e-5f)
                    {
                        lockedAtFloor = true;
                    }

                    prevK = k;
                }

                if (!monotonic)
                {
                    Record("D", "P1", true, true,
                        $"{regionTemplate.DisplayName}: CarryingCapacity not monotonically non-increasing as Water↓");
                }
                else
                {
                    Console.WriteLine("  Curve: WaterAvailability ↓ → K ↓ (monotonic non-increasing): YES");
                }

                if (lockedAtFloor)
                {
                    Record("D", "P2", false, false,
                        $"{regionTemplate.DisplayName}: At Water≤10% (incl. 0%), K.WaterFactor locks at floor 0.05. " +
                        "K remains >0 (not zero). Continuous but hard-floored — check if 0.05 floor is intended intensity.");
                }
            }

            Console.WriteLine();
        }

        // ================================================================
        // TEST E — Water → Food
        // ================================================================
        static void TestE_WaterToFood()
        {
            Section("TEST E — Water → Food Production");
            Console.WriteLine("Fixed Population/Season/Weather/Harvest; vary water to hit target WaterFactors.");
            Console.WriteLine();

            float[] targetWf = { 1.00f, 0.75f, 0.50f, 0.25f, 0.10f, 0.00f };
            var world = NewWorld(1f, 1f, 1f, 1f);

            foreach (var regionTemplate in world.State.Regions)
            {
                var race = FindRace(world.Races, regionTemplate.DominantRace);
                Console.WriteLine($"--- {regionTemplate.DisplayName} ---");

                float prevFood = float.NaN;
                bool monotonic = true;
                float foodAt1 = 0f;
                float foodAt0 = 0f;

                foreach (float wfTarget in targetWf)
                {
                    var region = regionTemplate.Clone();
                    region.WeatherFactor = 1f;
                    region.Influence.HarvestBlessing = 1f;
                    // Clear disasters so event mul = 1.
                    region.ActiveEvents = new List<RegionEvent>();

                    float labor = ResourceSystem.ComputeLabor(region, world.Config);
                    float tech = world.Config.TechBase + region.Education * world.Config.TechFromEducation;
                    float unconstrained = ResourceSystem.CalculateUnconstrainedFoodProduction(
                        region, race, SeasonId.Spring, world.Config, labor, tech, 1f, 1f);

                    SetWaterForTargetWaterFactor(region, unconstrained, world.Config, wfTarget);

                    ResourceSystem.ResolveWaterAllocation(region, unconstrained, world.Config,
                        out float wf, out _, out _, out _);
                    float actualFood = unconstrained * wf;
                    float foodCons = ResourceSystem.DailyFoodConsumption(region, world.Config);
                    float spoilRate = world.Config.FoodBaseSpoilageRate * world.Config.FoodSpoilageModifier(SeasonId.Spring);
                    float spoil = region.Get(ResourceId.Food) * spoilRate;
                    float net = actualFood - foodCons - spoil;

                    Console.WriteLine(
                        $"  TargetWF={wfTarget:0.00} ActualWF={F4(wf)} WaterStock={F(region.Get(ResourceId.Water))}  " +
                        $"BaseFoodProd={F4(unconstrained)} ActualFoodProd={F4(actualFood)}  " +
                        $"Spoilage≈{F4(spoil)} Consumption={F4(foodCons)} FoodNet≈{F4(net)}");

                    if (Math.Abs(wfTarget - 1f) < 1e-6f) foodAt1 = actualFood;
                    if (Math.Abs(wfTarget - 0f) < 1e-6f) foodAt0 = actualFood;

                    if (!float.IsNaN(prevFood) && actualFood > prevFood + 1e-3f)
                    {
                        monotonic = false;
                    }

                    prevFood = actualFood;
                }

                if (!monotonic)
                {
                    Record("E", "P1", true, true,
                        $"{regionTemplate.DisplayName}: FoodProduction not monotonically non-increasing as WaterFactor↓");
                }

                if (foodAt0 > foodAt1 * 0.05f && foodAt1 > 1f)
                {
                    Record("E", "P1", true, true,
                        $"{regionTemplate.DisplayName}: Water=0 / WF≈0 but FoodProduction still {F4(foodAt0)} " +
                        $"(>{5}% of full {F4(foodAt1)}) — must not keep full capacity when dry.");
                }
                else
                {
                    Console.WriteLine($"  Water=0 → FoodProduction≈{F4(foodAt0)} (full={F4(foodAt1)}): OK near-zero");
                }

                // Mild shortage should not cliff: check ContinuousWaterFactor at ratio=0.5
                float mid = ResourceSystem.ContinuousWaterFactor(0.5f, world.Config.AgriculturalWaterFactorSteepness);
                if (mid < 0.05f)
                {
                    Record("E", "P1", true, true,
                        $"{regionTemplate.DisplayName}: mild water shortage cliffs — ContinuousWaterFactor(0.5)={F4(mid)}");
                }
                else
                {
                    Console.WriteLine($"  ContinuousWaterFactor(0.5)={F4(mid)} (not a cliff to 0): OK");
                }
            }

            Console.WriteLine();
        }

        // ================================================================
        // TEST F — Death decomposition at Fertility ×1.30
        // ================================================================
        static void TestF_DeathDecomposition()
        {
            Section("TEST F — Death Decomposition (Fertility×1.30)");
            Console.WriteLine("Fixed: Harvest×1.00 Disease×1.00 Stability×1.00  Days=360");
            Console.WriteLine();

            var world = NewWorld(1.30f, 1f, 1f, 1f);
            var cum = new CumMap();
            for (int d = 0; d < 360; d++)
            {
                AdvanceAccumulate(world, cum);
            }

            foreach (var r in world.State.Regions)
            {
                var c = cum.For(r.Id);
                float bd = c.TotalDeaths > 1e-6f ? c.Births / c.TotalDeaths : float.PositiveInfinity;
                float dShare = c.TotalDeaths > 1e-6f ? c.DiseaseDeaths / c.TotalDeaths : 0f;
                float nShare = c.TotalDeaths > 1e-6f ? c.NaturalDeaths / c.TotalDeaths : 0f;
                float oShare = c.TotalDeaths > 1e-6f ? c.OverCapacityDeaths / c.TotalDeaths : 0f;

                Console.WriteLine($"--- {r.DisplayName} FinalPop={F(r.Population)} ---");
                Console.WriteLine($"  CumBirths={F(c.Births)}");
                Console.WriteLine($"  NaturalDeaths={F(c.NaturalDeaths)} ({F4(nShare)})");
                Console.WriteLine($"  DiseaseDeaths={F(c.DiseaseDeaths)} ({F4(dShare)})");
                Console.WriteLine($"  OtherDeaths/OverCapacity={F(c.OverCapacityDeaths)} ({F4(oShare)})");
                Console.WriteLine($"  TotalDeaths={F(c.TotalDeaths)}");
                Console.WriteLine($"  Birth/TotalDeaths={F4(bd)}");

                float natPlusDis = c.NaturalDeaths + c.DiseaseDeaths;
                if (natPlusDis > c.Births * 1.15f)
                {
                    Record("F", "P2", false, false,
                        $"{r.DisplayName}: Even with Fertility×1.30, Natural+Disease deaths ({F(natPlusDis)}) " +
                        $">> Births ({F(c.Births)}). Birth/Death={F4(bd)}. " +
                        $"Disease share={F4(dShare)}, Natural share={F4(nShare)}. " +
                        "Explains population decline — balance observation, do not retune in P2-A2.");
                }
            }

            Console.WriteLine();
        }

        // ================================================================
        // TEST G — Disease single variable
        // ================================================================
        static void TestG_DiseaseSingleVariable()
        {
            Section("TEST G — Disease Single Variable");
            Console.WriteLine("Fixed: Fertility×1.30 Harvest×1.00 Stability×1.00  Days=360");
            Console.WriteLine("Arms: Disease ×0.70 / ×1.00 / ×1.30");
            Console.WriteLine();

            float[] diseases = { 0.70f, 1.00f, 1.30f };
            string[] labels = { "Lo", "Mid", "Hi" };
            var arms = new List<(string Label, float Dis, CumMap Cum, HeadlessWorld World)>();

            for (int i = 0; i < diseases.Length; i++)
            {
                var world = NewWorld(1.30f, 1f, diseases[i], 1f);
                var cum = new CumMap();
                for (int d = 0; d < 360; d++)
                {
                    AdvanceAccumulate(world, cum);
                }

                arms.Add((labels[i], diseases[i], cum, world));
            }

            foreach (RegionId id in Enum.GetValues(typeof(RegionId)))
            {
                Console.WriteLine($"--- Region {id} ---");
                for (int i = 0; i < arms.Count; i++)
                {
                    var a = arms[i];
                    var c = a.Cum.For(id);
                    var r = a.World.Region(id);
                    Console.WriteLine(
                        $"  {a.Label} Dis×{a.Dis:0.00}: DiseaseDeaths={F(c.DiseaseDeaths)} TotalDeaths={F(c.TotalDeaths)}  " +
                        $"Births={F(c.Births)} FinalPop={F(r.Population)} Net={F(c.NetChange)}");
                }

                float dd0 = arms[0].Cum.For(id).DiseaseDeaths;
                float dd1 = arms[1].Cum.For(id).DiseaseDeaths;
                float dd2 = arms[2].Cum.For(id).DiseaseDeaths;
                float pop0 = arms[0].World.Region(id).Population;
                float pop1 = arms[1].World.Region(id).Population;
                float pop2 = arms[2].World.Region(id).Population;

                bool deathsMono = dd0 <= dd1 + 1f && dd1 <= dd2 + 1f;
                bool popMono = pop0 + 1f >= pop1 && pop1 + 1f >= pop2; // higher disease → lower or equal pop
                Console.WriteLine($"  DiseaseDeaths ↑ with Disease modifier: {(deathsMono ? "YES" : "NO")}");
                Console.WriteLine($"  Population decline ↑ (FinalPop ↓) with Disease: {(popMono ? "YES" : "NO")}");

                if (!deathsMono)
                {
                    Record("G", "P1", true, true, $"{id}: DiseaseDeaths reverse relationship vs Disease modifier");
                }

                if (!popMono)
                {
                    Record("G", "P1", true, true, $"{id}: Population reverse relationship vs Disease modifier");
                }
            }

            Console.WriteLine();
        }

        // ================================================================
        // TEST H — Long-term numeric stability (100y)
        // ================================================================
        static void TestH_LongTermNumericStability()
        {
            Section("TEST H — Long-term Numeric Stability (100 years = 36000 days)");
            Console.WriteLine("Scenarios: Normal 1/1/1/1 | Extreme 0.70/0.70/1.30/0.70 | HighFert 1.30/1.00/1.00/0.70");
            Console.WriteLine();

            RunLongTerm("Normal", 1f, 1f, 1f, 1f, 36000);
            RunLongTerm("Extreme", 0.70f, 0.70f, 1.30f, 0.70f, 36000);
            RunLongTerm("HighFertility", 1.30f, 1f, 1f, 0.70f, 36000);
            Console.WriteLine();
        }

        static void RunLongTerm(string name, float fert, float harvest, float disease, float stab, int days)
        {
            Console.WriteLine($"--- {name} Fert×{fert:0.00} Harv×{harvest:0.00} Dis×{disease:0.00} Stab×{stab:0.00} ---");
            var world = NewWorld(fert, harvest, disease, stab);
            int reportEvery = 3600; // 10 years
            float[] prevPop = CapturePopsArray(world);

            for (int d = 1; d <= days; d++)
            {
                // Snapshot previous finite values for error reporting.
                var prevSnapshots = new List<(RegionId Id, string Var, float Val)>();
                foreach (var r in world.State.Regions)
                {
                    prevSnapshots.Add((r.Id, "Population", r.Population));
                    prevSnapshots.Add((r.Id, "Food", r.Get(ResourceId.Food)));
                    prevSnapshots.Add((r.Id, "Water", r.Get(ResourceId.Water)));
                }

                world.AdvanceDay();

                if (world.State.HaltedOnNumericError)
                {
                    Console.WriteLine($"  HALT Year={world.State.Year} Day={world.State.DayOfYear} TotalDay={world.State.TotalDays}");
                    Console.WriteLine($"  {world.State.LastNumericError}");
                    Record("H", "P0", true, true,
                        $"{name}: numeric halt at TotalDay={world.State.TotalDays}: {world.State.LastNumericError}");
                    return;
                }

                foreach (var r in world.State.Regions)
                {
                    CheckFinite(name, world, r, "Population", r.Population, fert);
                    CheckFinite(name, world, r, "Food", r.Get(ResourceId.Food), fert);
                    CheckFinite(name, world, r, "Water", r.Get(ResourceId.Water), fert);
                    CheckFinite(name, world, r, "Timber", r.Get(ResourceId.Timber), fert);
                    CheckFinite(name, world, r, "Ore", r.Get(ResourceId.Ore), fert);
                    CheckFinite(name, world, r, "Faith", r.Get(ResourceId.Faith), fert);
                    CheckFinite(name, world, r, "Knowledge", r.Get(ResourceId.Knowledge), fert);
                    CheckFinite(name, world, r, "Mana", r.Get(ResourceId.Magic), fert);
                    CheckFinite(name, world, r, "Stability", r.Stability, fert);
                    CheckFinite(name, world, r, "Education", r.Education, fert);
                    CheckFinite(name, world, r, "Disease", r.DiseasePressure, fert);

                    if (world.State.HaltedOnNumericError)
                    {
                        return;
                    }
                }

                if (d % reportEvery == 0 || d == days)
                {
                    Console.Write($"  Y{world.State.Year} D{world.State.DayOfYear}:");
                    foreach (var r in world.State.Regions)
                    {
                        Console.Write($" {r.DisplayName}[Pop={F(r.Population)} Food={F(r.Get(ResourceId.Food))} " +
                                      $"Water={F(r.Get(ResourceId.Water))} Dis={F4(r.DiseasePressure)} Stab={F4(r.Stability)}]");
                    }

                    Console.WriteLine();
                }
            }

            Console.WriteLine($"  {name}: completed {days} days — all checked variables finite.");
        }

        static void CheckFinite(string scenario, HeadlessWorld world, RegionState r, string varName, float value, float fertMod)
        {
            if (NumericGuard.IsFinite(value))
            {
                return;
            }

            Console.WriteLine(
                $"  NON-FINITE Year={world.State.Year} Day={world.State.DayOfYear} Region={r.DisplayName} " +
                $"Variable={varName} NewValue={value} RelevantModifier=Fert×{fertMod:0.00}");
            Record("H", "P0", true, true,
                $"{scenario}/{r.DisplayName}/{varName} became {value} at TotalDay={world.State.TotalDays}");
            // Stop propagation is handled by NumericGuard in sim; flag finding here.
        }

        // ================================================================
        // TEST I — Event independence
        // ================================================================
        static void TestI_EventIndependence()
        {
            Section("TEST I — Regional Event Independence (360 days)");
            Console.WriteLine();

            var world = NewWorld(1f, 1f, 1f, 1f);
            var log = new List<string>();
            var sameDaySameType = new List<string>();

            for (int d = 0; d < 360; d++)
            {
                int dayBefore = world.State.TotalDays;
                world.AdvanceDay();
                int day = world.State.TotalDays; // after AdvanceCalendar, TotalDays incremented

                var started = new List<(RegionId Reg, SimEventType Type, RegionEvent Evt)>();
                foreach (var r in world.State.Regions)
                {
                    if (r.ActiveEvents == null) continue;
                    foreach (var e in r.ActiveEvents)
                    {
                        if (e.StartDay == day || e.StartDay == dayBefore)
                        {
                            // Newly started around this tick
                            if (e.StartDay == world.State.TotalDays ||
                                (e.EventType == SimEventType.NaturalDisaster && e.StartDay == day))
                            {
                                // Dedup by EventId
                            }
                        }

                        if (e.StartDay == day)
                        {
                            started.Add((r.Id, e.EventType, e));
                            log.Add(
                                $"Day={e.StartDay} Region={r.DisplayName} EventType={e.EventType} " +
                                $"Severity={F4(e.Severity)} StartDay={e.StartDay} EndDay={e.EndDay} Duration={e.Duration}");
                        }
                    }
                }

                // Detect same-day same-type across regions
                for (int i = 0; i < started.Count; i++)
                {
                    for (int j = i + 1; j < started.Count; j++)
                    {
                        if (started[i].Type == started[j].Type && started[i].Type == SimEventType.NaturalDisaster)
                        {
                            float rollI = EventSystem.Hash01(Seed, started[i].Evt.StartDay, (int)started[i].Reg, 3);
                            float rollJ = EventSystem.Hash01(Seed, started[j].Evt.StartDay, (int)started[j].Reg, 3);
                            sameDaySameType.Add(
                                $"Day={started[i].Evt.StartDay} {started[i].Type}: {started[i].Reg}(roll={F6(rollI)}) & " +
                                $"{started[j].Reg}(roll={F6(rollJ)}) threshold={world.Config.NaturalDisasterChancePerDay} " +
                                $"Seed={Seed} — independent Hash01(region) rolls");
                        }
                    }
                }
            }

            Console.WriteLine($"Events recorded: {log.Count}");
            foreach (var line in log)
            {
                Console.WriteLine("  " + line);
            }

            if (sameDaySameType.Count > 0)
            {
                Console.WriteLine("Same-day NaturalDisaster across regions (investigate independence):");
                foreach (var s in sameDaySameType)
                {
                    Console.WriteLine("  " + s);
                }

                Record("I", "P2", false, false,
                    $"Observed {sameDaySameType.Count} same-day multi-region NaturalDisaster pair(s). " +
                    "Hash01 includes regionId — likely independent coincidence, not shared state copy. See rolls above.");
            }
            else
            {
                Console.WriteLine("No same-day NaturalDisaster across multiple regions in 360d.");
            }

            // Structural independence: rolls differ by region
            bool differ = false;
            for (int day = 1; day <= 360; day++)
            {
                float a = EventSystem.Hash01(Seed, day, (int)RegionId.Empire, 3);
                float b = EventSystem.Hash01(Seed, day, (int)RegionId.Theocracy, 3);
                float c = EventSystem.Hash01(Seed, day, (int)RegionId.Sea, 3);
                if (Math.Abs(a - b) > 1e-6f || Math.Abs(b - c) > 1e-6f)
                {
                    differ = true;
                    break;
                }
            }

            Console.WriteLine($"Hash01 differs by RegionId: {(differ ? "YES (independent rolls)" : "NO")}");
            if (!differ)
            {
                Record("I", "P1", true, true, "Event Hash01 identical across regions — shared state bug");
            }

            Console.WriteLine();
        }

        // ================================================================
        // TEST J — Disaster duration / food recovery
        // ================================================================
        static void TestJ_DisasterDuration()
        {
            Section("TEST J — NaturalDisaster Duration & FoodProduction Recovery");
            Console.WriteLine($"Config: NaturalDisasterDuration={SimulationConfig.CreateDefault().NaturalDisasterDuration} " +
                              $"FoodPenalty={SimulationConfig.CreateDefault().NaturalDisasterFoodProductionPenalty}");
            Console.WriteLine();

            var world = NewWorld(1f, 1f, 1f, 1f);
            var disasters = new List<RegionEvent>();
            var foodDuring = new List<string>();

            // Run 720 days to catch cross-season / cross-year disasters
            for (int d = 0; d < 720; d++)
            {
                world.AdvanceDay();
                foreach (var r in world.State.Regions)
                {
                    if (r.ActiveEvents == null) continue;
                    foreach (var e in r.ActiveEvents)
                    {
                        if (e.EventType != SimEventType.NaturalDisaster) continue;
                        if (e.StartDay != world.State.TotalDays) continue;

                        disasters.Add(new RegionEvent
                        {
                            EventId = e.EventId,
                            EventType = e.EventType,
                            RegionId = r.Id,
                            StartDay = e.StartDay,
                            Duration = e.Duration,
                            Severity = e.Severity
                        });

                        float mul = EventSystem.GetFoodProductionEventModifier(r, world.State.TotalDays, world.Config);
                        foodDuring.Add(
                            $"Start Day={e.StartDay} Region={r.DisplayName} Duration={e.Duration} EndDay={e.EndDay} " +
                            $"Severity={F4(e.Severity)} FoodProdModifier={F4(mul)} " +
                            $"Season={world.State.CurrentSeason} Year={world.State.Year}");
                    }
                }
            }

            Console.WriteLine($"NaturalDisasters observed in 720d: {disasters.Count}");
            foreach (var line in foodDuring)
            {
                Console.WriteLine("  " + line);
            }

            // Inject controlled disaster to verify expiry + food recovery across season boundary
            {
                var w2 = NewWorld(1f, 1f, 1f, 1f);
                // Advance to near end of spring so disaster spans into summer
                w2.AdvanceDays(80);
                var region = w2.Region(RegionId.Empire);
                var race = FindRace(w2.Races, region.DominantRace);
                var evt = new RegionEvent
                {
                    EventId = "harness-disaster",
                    EventType = SimEventType.NaturalDisaster,
                    RegionId = RegionId.Empire,
                    Scope = SimEventScope.Regional,
                    StartDay = w2.State.TotalDays,
                    Duration = w2.Config.NaturalDisasterDuration,
                    Severity = 1f
                };
                region.ActiveEvents.Add(evt);

                float mulStart = EventSystem.GetFoodProductionEventModifier(region, w2.State.TotalDays, w2.Config);
                float foodStart = ResourceSystem.CalculateFoodProduction(region, race, w2.State.CurrentSeason, w2.Config, w2.State.TotalDays);
                int startDay = evt.StartDay;
                int endDay = evt.EndDay;
                SeasonId startSeason = w2.State.CurrentSeason;

                // Advance through disaster
                while (w2.State.TotalDays < endDay)
                {
                    w2.AdvanceDay();
                }

                SeasonId endSeason = w2.State.CurrentSeason;
                bool stillActive = region.HasActiveEvent(SimEventType.NaturalDisaster, w2.State.TotalDays);
                float mulEnd = EventSystem.GetFoodProductionEventModifier(region, w2.State.TotalDays, w2.Config);
                float foodEnd = ResourceSystem.CalculateFoodProduction(region, race, w2.State.CurrentSeason, w2.Config, w2.State.TotalDays);

                // One more day past end
                w2.AdvanceDay();
                bool activeAfter = region.HasActiveEvent(SimEventType.NaturalDisaster, w2.State.TotalDays);
                float mulAfter = EventSystem.GetFoodProductionEventModifier(region, w2.State.TotalDays, w2.Config);

                Console.WriteLine("--- Controlled disaster (Empire, start near D80) ---");
                Console.WriteLine($"  StartDay={startDay} EndDay={endDay} Duration={evt.Duration}");
                Console.WriteLine($"  StartSeason={startSeason} EndSeason={endSeason} (cross-season={(startSeason != endSeason ? "YES" : "NO")})");
                Console.WriteLine($"  FoodModifier at start={F4(mulStart)} food≈{F4(foodStart)}");
                Console.WriteLine($"  At EndDay active={stillActive} FoodModifier={F4(mulEnd)} food≈{F4(foodEnd)}");
                Console.WriteLine($"  After expiry active={activeAfter} FoodModifier={F4(mulAfter)}");

                if (evt.Duration <= 0)
                {
                    Record("J", "P1", true, true, "NaturalDisaster has no End condition (Duration<=0)");
                }

                if (activeAfter || Math.Abs(mulAfter - 1f) > 1e-4f)
                {
                    Record("J", "P1", true, true,
                        $"NaturalDisaster did not fully expire: activeAfter={activeAfter} FoodModifier={F4(mulAfter)}");
                }
                else
                {
                    Console.WriteLine("  Expiry + FoodProduction modifier restore: OK");
                }

                // Permanent check: no disaster lasting > Duration+1 in the wild log
                foreach (var e in disasters)
                {
                    if (e.Duration != world.Config.NaturalDisasterDuration && e.Duration > world.Config.NaturalDisasterDuration * 2)
                    {
                        Record("J", "P1", true, true,
                            $"Disaster Duration={e.Duration} unexpected (config={world.Config.NaturalDisasterDuration})");
                    }
                }
            }

            Console.WriteLine();
        }

        // ================================================================
        // FastForward 360
        // ================================================================
        static void TestFastForward_360()
        {
            Section("FastForward Acceptance — Daily 360 vs FastForward 360");
            Console.WriteLine("No threshold tuning. Report real errors.");
            Console.WriteLine();

            var world = NewWorld(1f, 1f, 1f, 1f);
            var report = FastForwardConsistencyTest.Run(world.State.Clone(), world.Races, world.Config, 360);
            Console.WriteLine(report.Text);

            foreach (var m in report.Metrics)
            {
                Console.WriteLine(
                    $"  {m.Name}: Daily={F(m.Daily)} Fast={F(m.Fast)} AbsDiff={F(m.AbsDiff)} RelError={m.ErrorPct * 100f:0.00}%");
                if (m.ErrorPct > 0.05f)
                {
                    Record("FF360", m.ErrorPct > 0.25f ? "P1" : "P2", m.ErrorPct > 0.25f, true,
                        $"{m.Name} Daily vs Fast error={m.ErrorPct * 100f:0.0}% " +
                        $"(Daily={F(m.Daily)} Fast={F(m.Fast)}). Do not retune thresholds to PASS.");
                }
            }

            if (!report.Finite)
            {
                Record("FF360", "P0", true, true, "Daily or Fast path produced NaN/Infinity");
            }

            Console.WriteLine();
        }

        // ================================================================
        // FastForward long (720 + optional 3600)
        // ================================================================
        static void TestFastForward_Long()
        {
            Section("FastForward Long — error accumulation");
            Console.WriteLine();

            float err360Pop = float.NaN, err720Pop = float.NaN, err3600Pop = float.NaN;
            float err360Food = float.NaN, err720Food = float.NaN, err3600Food = float.NaN;
            float err360Stab = float.NaN, err720Stab = float.NaN, err3600Stab = float.NaN;

            void Capture(int days, ref float pop, ref float food, ref float stab)
            {
                var world = NewWorld(1f, 1f, 1f, 1f);
                var report = FastForwardConsistencyTest.Run(world.State.Clone(), world.Races, world.Config, days);
                Console.WriteLine(report.Text);
                foreach (var m in report.Metrics)
                {
                    if (m.Name == "PopTotal") pop = m.ErrorPct;
                    if (m.Name == "FoodTotal") food = m.ErrorPct;
                    if (m.Name == "StabilityAvg") stab = m.ErrorPct;
                }

                if (!report.Finite)
                {
                    Record("FFLong", "P0", true, true, $"Daily vs Fast {days}d non-finite");
                }
            }

            Console.WriteLine("--- 360d (baseline reference) ---");
            Capture(360, ref err360Pop, ref err360Food, ref err360Stab);

            Console.WriteLine("--- 720d ---");
            Capture(720, ref err720Pop, ref err720Food, ref err720Stab);

            Console.WriteLine("--- 3600d (10 years) ---");
            Capture(3600, ref err3600Pop, ref err3600Food, ref err3600Stab);

            Console.WriteLine("Error accumulation:");
            Console.WriteLine($"  PopTotal:    360d={Pct(err360Pop)}  720d={Pct(err720Pop)}  3600d={Pct(err3600Pop)}");
            Console.WriteLine($"  FoodTotal:   360d={Pct(err360Food)}  720d={Pct(err720Food)}  3600d={Pct(err3600Food)}");
            Console.WriteLine($"  StabilityAvg:360d={Pct(err360Stab)}  720d={Pct(err720Stab)}  3600d={Pct(err3600Stab)}");

            if (!float.IsNaN(err360Pop) && !float.IsNaN(err3600Pop) && err3600Pop > err360Pop * 3f && err3600Pop > 0.3f)
            {
                Record("FFLong", "P1", true, true,
                    $"FastForward Pop error accumulates: 1y={Pct(err360Pop)} → 10y={Pct(err3600Pop)}. " +
                    "Suggests projection drift, not just noise.");
            }
            else if (!float.IsNaN(err720Pop) && err720Pop > err360Pop * 1.5f && err720Pop > 0.15f)
            {
                Record("FFLong", "P2", false, true,
                    $"FastForward Pop error grows 1y={Pct(err360Pop)} → 2y={Pct(err720Pop)}.");
            }

            Console.WriteLine();
        }

        // ================================================================
        // Helpers
        // ================================================================
        static HeadlessWorld NewWorld(float fertility, float harvest, float disease, float stability)
        {
            var world = new HeadlessWorld(Seed);
            world.ApplyGlobalInfluence(fertility, harvest, disease, stability);
            return world;
        }

        static void PrintDay0Basics(HeadlessWorld world)
        {
            Console.WriteLine("Day 0 baselines:");
            foreach (var r in world.State.Regions)
            {
                var race = FindRace(world.Races, r.DominantRace);
                var m = MirrorPopulation(r, race, world.State.CurrentSeason, world.Config);
                Console.WriteLine(
                    $"  {r.DisplayName}: Pop={F(m.Population)} K={F(m.CarryingCapacity)} Pop/K={F4(m.PopOverK)}  " +
                    $"BaseBirthRate={F6(m.BaseBirthRate)} FertMod={F4(m.FertilityModifier)} EffBirthRate={F6(m.EffectiveBirthRate)}  " +
                    $"Food={F(r.Get(ResourceId.Food))} Water={F(r.Get(ResourceId.Water))} Dis={F4(r.DiseasePressure)}");
            }

            Console.WriteLine();
        }

        static void PrintCheckpoint(string test, HeadlessWorld world, CumMap cum, Dictionary<RegionId, float> day0, int day)
        {
            Console.WriteLine($"=== {test} Checkpoint D{day} Year={world.State.Year} Season={world.State.CurrentSeason} ===");
            foreach (var r in world.State.Regions)
            {
                var race = FindRace(world.Races, r.DominantRace);
                var m = MirrorPopulation(r, race, world.State.CurrentSeason, world.Config);
                var c = cum.For(r.Id);
                float bd = c.TotalDeaths > 1e-6f ? c.Births / c.TotalDeaths : float.PositiveInfinity;
                Console.WriteLine($"  [{r.DisplayName}]");
                Console.WriteLine($"    Population={F(m.Population)} (Day0={F(day0[r.Id])})  CarryingCapacity={F(m.CarryingCapacity)}  Pop/K={F4(m.PopOverK)}");
                Console.WriteLine($"    BaseBirthRate={F6(m.BaseBirthRate)} FertilityModifier={F4(m.FertilityModifier)} EffectiveBirthRate={F6(m.EffectiveBirthRate)} Births(today)={F4(m.Births)}");
                Console.WriteLine($"    NaturalDeaths(today)={F4(m.NaturalDeaths)} DiseaseDeaths(today)={F4(m.DiseaseDeaths)} Other/OverCap(today)={F4(m.OverCapacityDeaths)} TotalDeaths(today)={F4(m.TotalDeaths)}");
                Console.WriteLine($"    NetPopulationChange(today)={F4(m.NetChange)}  CumBirths={F(c.Births)} CumDeaths={F(c.TotalDeaths)} CumNet={F(c.NetChange)}");
                Console.WriteLine($"    Birth/Death Ratio(cum)={F4(bd)}  LogisticBirthFactor={F4(m.LogisticBirthFactor)}");
            }

            Console.WriteLine();
        }

        static void AdvanceAccumulate(HeadlessWorld world, CumMap cum)
        {
            float[] popBefore = new float[world.State.Regions.Length];
            for (int i = 0; i < world.State.Regions.Length; i++)
            {
                popBefore[i] = world.State.Regions[i].Population;
            }

            // Mirror overpop before tick using pre-tick state (matches TickDay inputs).
            var overBefore = new float[world.State.Regions.Length];
            for (int i = 0; i < world.State.Regions.Length; i++)
            {
                var r = world.State.Regions[i];
                var race = FindRace(world.Races, r.DominantRace);
                var m = MirrorPopulation(r, race, world.State.CurrentSeason, world.Config);
                overBefore[i] = m.OverCapacityDeaths;
            }

            world.AdvanceDay();

            for (int i = 0; i < world.State.Regions.Length; i++)
            {
                var r = world.State.Regions[i];
                float naturalPlusOver = r.LastNaturalDeath;
                float disease = r.LastDiseaseDeath;
                float over = overBefore[i]; // approx; K may shift slightly during resource tick before pop tick
                // Recompute over using LastCarryingCapacity after tick for better match
                if (r.LastCarryingCapacity > 0f && popBefore[i] > r.LastCarryingCapacity)
                {
                    over = popBefore[i] * world.Config.OverpopulationDeathRate * (popBefore[i] / r.LastCarryingCapacity - 1f);
                }
                else
                {
                    over = 0f;
                }

                float natural = Mathf.Max(0f, naturalPlusOver - over);
                float births = (r.Population - popBefore[i]) + naturalPlusOver + disease;
                if (births < 0f) births = 0f;

                var c = cum.For(r.Id);
                c.Births += births;
                c.NaturalDeaths += natural;
                c.DiseaseDeaths += disease;
                c.OverCapacityDeaths += over;
                c.NetChange += r.Population - popBefore[i];
            }
        }

        static DayMetrics MirrorPopulation(RegionState region, RaceDefinition race, SeasonId season, SimulationConfig config)
        {
            var influence = region.Influence ?? new RegionObserverInfluence();
            float pop = region.Population;
            float carrying = PopulationSystem.CalculateCarryingCapacity(region, race, season, config);

            float foodRatio = pop <= 0f
                ? 0f
                : Mathf.Clamp01(region.Get(ResourceId.Food) / Mathf.Max(1f, pop * config.FoodRatioSoftCap));
            float fertilityModifier = SanitizePositive(influence.FertilityBlessing, 1f);
            float baseBirthRate = config.BaseFertility * race.FertilityFactor;
            float fertility = baseBirthRate * fertilityModifier;

            float logistic = 1f - pop / Mathf.Max(1f, carrying);
            float logisticBirthFactor = Mathf.Max(0f, Mathf.Min(logistic, 1.5f));
            float effectiveBirthRate = fertility * (0.5f + foodRatio) * logisticBirthFactor;
            float births = pop * effectiveBirthRate;

            float naturalDeaths = pop
                                  * (config.BaseNaturalDeath / Mathf.Max(0.05f, race.LifespanFactor))
                                  * config.DeathModifier(season);
            float diseaseDeaths = pop
                                  * region.DiseasePressure
                                  * config.DiseaseDeathRate
                                  * config.DiseaseModifier(season)
                                  * SanitizePositive(influence.DiseasePressure, 1f);
            float overCapacityDeaths = 0f;
            if (pop > carrying && carrying > 0f)
            {
                overCapacityDeaths = pop * config.OverpopulationDeathRate * (pop / carrying - 1f);
            }

            float totalDeaths = naturalDeaths + diseaseDeaths + overCapacityDeaths;
            return new DayMetrics
            {
                Population = pop,
                CarryingCapacity = carrying,
                PopOverK = pop / Mathf.Max(1f, carrying),
                BaseBirthRate = baseBirthRate,
                FertilityModifier = fertilityModifier,
                EffectiveBirthRate = effectiveBirthRate,
                LogisticBirthFactor = logisticBirthFactor,
                Births = births,
                NaturalDeaths = naturalDeaths,
                DiseaseDeaths = diseaseDeaths,
                OverCapacityDeaths = overCapacityDeaths,
                TotalDeaths = totalDeaths,
                NetChange = births - totalDeaths
            };
        }

        static Dictionary<RegionId, DayMetrics> CaptureAllMetrics(HeadlessWorld world)
        {
            var map = new Dictionary<RegionId, DayMetrics>();
            foreach (var r in world.State.Regions)
            {
                var race = FindRace(world.Races, r.DominantRace);
                map[r.Id] = MirrorPopulation(r, race, world.State.CurrentSeason, world.Config);
            }

            return map;
        }

        static Dictionary<RegionId, float> CapturePops(HeadlessWorld world)
        {
            var map = new Dictionary<RegionId, float>();
            foreach (var r in world.State.Regions)
            {
                map[r.Id] = r.Population;
            }

            return map;
        }

        static float[] CapturePopsArray(HeadlessWorld world)
        {
            var a = new float[world.State.Regions.Length];
            for (int i = 0; i < a.Length; i++) a[i] = world.State.Regions[i].Population;
            return a;
        }

        /// <summary>
        /// Sets WaterStock so ResolveWaterAllocation yields approximately target WaterFactor.
        /// Does not modify formulas — only constructs input stock.
        /// </summary>
        static void SetWaterForTargetWaterFactor(
            RegionState region,
            float unconstrainedFood,
            SimulationConfig config,
            float targetWf)
        {
            float livingNeed = Mathf.Max(0f, region.Population) * config.WaterNeedPerCapita;
            float agriNeed = Mathf.Max(0f, unconstrainedFood) * config.AgriculturalWaterPerFoodUnit;

            if (targetWf <= 0f)
            {
                region.Set(ResourceId.Water, 0f);
                return;
            }

            if (targetWf >= 1f || agriNeed <= 1e-8f)
            {
                region.Set(ResourceId.Water, livingNeed + agriNeed + 1f);
                return;
            }

            // Invert ContinuousWaterFactor: wf = 1 - exp(-k*ratio) => ratio = -ln(1-wf)/k
            float k = Mathf.Max(0.1f, config.AgriculturalWaterFactorSteepness);
            float ratio = -(float)Math.Log(Mathf.Max(1e-6f, 1f - targetWf)) / k;
            ratio = Mathf.Clamp(ratio, 0f, 1f);
            float remaining = agriNeed * ratio;
            region.Set(ResourceId.Water, livingNeed + remaining);
        }

        static RaceDefinition FindRace(RaceDefinition[] races, RaceId id)
        {
            foreach (var r in races)
            {
                if (r.Id == id) return r;
            }

            return races[0];
        }

        static float SanitizePositive(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f) return fallback;
            return value;
        }

        static void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine("############################################################");
            Console.WriteLine("# " + title);
            Console.WriteLine("############################################################");
        }

        static void Header(string title)
        {
            Console.WriteLine("================================================================");
            Console.WriteLine(title);
            Console.WriteLine("================================================================");
        }

        static void Record(string test, string priority, bool isBug, bool needsFormulaChange, string detail)
        {
            Findings.Add(new Finding
            {
                Test = test,
                Priority = priority,
                IsBug = isBug,
                NeedsFormulaChange = needsFormulaChange,
                Detail = detail
            });
        }

        static void PrintFindingsSummary()
        {
            Section("FINDINGS SUMMARY (classified — not auto-fixed)");
            if (Findings.Count == 0)
            {
                Console.WriteLine("No findings recorded.");
                return;
            }

            int i = 1;
            foreach (var f in Findings)
            {
                Console.WriteLine($"{i}. [{f.Priority}] Test={f.Test} Bug={(f.IsBug ? "YES" : "NO")} " +
                                  $"NeedsFormulaChange={(f.NeedsFormulaChange ? "YES" : "NO / observe")}");
                Console.WriteLine($"   {f.Detail}");
                Console.WriteLine($"   Suggested priority: {f.Priority}");
                i++;
            }

            Console.WriteLine();
            Console.WriteLine("Priority legend: P0=NaN/Inf  P1=core math relation  P2=balance  P3=UI");
            Console.WriteLine("P2-A2 does not modify formulas — decide which findings enter formal fix.");
        }

        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static string F4(float v) => v.ToString("0.####", CultureInfo.InvariantCulture);
        static string F6(float v) => v.ToString("0.######", CultureInfo.InvariantCulture);
        static string Pct(float err) => float.IsNaN(err) ? "n/a" : (err * 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%";

        sealed class CumTotals
        {
            public float Births;
            public float NaturalDeaths;
            public float DiseaseDeaths;
            public float OverCapacityDeaths;
            public float NetChange;
            public float TotalDeaths => NaturalDeaths + DiseaseDeaths + OverCapacityDeaths;
        }

        sealed class CumMap
        {
            readonly Dictionary<RegionId, CumTotals> _map = new Dictionary<RegionId, CumTotals>();

            public CumTotals For(RegionId id)
            {
                if (!_map.TryGetValue(id, out var c))
                {
                    c = new CumTotals();
                    _map[id] = c;
                }

                return c;
            }
        }

        struct DayMetrics
        {
            public float Population;
            public float CarryingCapacity;
            public float PopOverK;
            public float BaseBirthRate;
            public float FertilityModifier;
            public float EffectiveBirthRate;
            public float LogisticBirthFactor;
            public float Births;
            public float NaturalDeaths;
            public float DiseaseDeaths;
            public float OverCapacityDeaths;
            public float TotalDeaths;
            public float NetChange;
        }

        sealed class Finding
        {
            public string Test;
            public string Priority;
            public bool IsBug;
            public bool NeedsFormulaChange;
            public string Detail;
        }
    }
}
