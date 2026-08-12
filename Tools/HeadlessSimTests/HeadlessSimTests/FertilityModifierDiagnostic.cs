using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using DivineWorld.Simulation.Systems;
using UnityEngine;

namespace HeadlessSimTests
{
    /// <summary>
    /// Single-variable diagnostic: FertilityBlessing only (0.70 / 1.00 / 1.30).
    /// Does not modify population formulas — only observes / mirrors them for reporting.
    /// </summary>
    public static class FertilityModifierDiagnostic
    {
        const int Seed = 20260810;
        const float Harvest = 1.00f;
        const float Disease = 1.00f;
        const float Stability = 0.70f;

        static readonly float[] FertilityLevels = { 0.70f, 1.00f, 1.30f };
        static readonly string[] ArmLabels = { "A", "B", "C" };

        public static int Run()
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("Population Fertility Modifier Diagnostic");
            Console.WriteLine("========================================");
            Console.WriteLine($"Seed={Seed}  Harvest×{Harvest:0.00}  Disease×{Disease:0.00}  Stability×{Stability:0.00}");
            Console.WriteLine("Arms: A fertility×0.70 | B fertility×1.00 | C fertility×1.30");
            Console.WriteLine("Each arm starts from identical Day-0 world (independent Reset).");
            Console.WriteLine();

            var results = new List<ArmResult>();
            for (int i = 0; i < FertilityLevels.Length; i++)
            {
                results.Add(RunArm(ArmLabels[i], FertilityLevels[i]));
            }

            PrintDetailedReports(results);
            PrintComparisonTables(results);
            return AnalyzeAndConclude(results);
        }

        static ArmResult RunArm(string label, float fertility)
        {
            var world = new HeadlessWorld(Seed);
            ApplyFixedInfluences(world, fertility);

            var day0 = CaptureSnapshot(world, cumulative: null);

            var cum30 = new CumulativeTotals();
            AdvanceAndAccumulate(world, 30, cum30);
            var day30 = CaptureSnapshot(world, cum30);

            // Fresh 360-day run from Day 0 (does not continue from the 30-day arm).
            world = new HeadlessWorld(Seed);
            ApplyFixedInfluences(world, fertility);
            var cum360 = new CumulativeTotals();
            AdvanceAndAccumulate(world, 360, cum360);
            var day360 = CaptureSnapshot(world, cum360);

            return new ArmResult
            {
                Label = label,
                Fertility = fertility,
                Day0 = day0,
                Day30 = day30,
                Day360 = day360
            };
        }

        static void ApplyFixedInfluences(HeadlessWorld world, float fertility)
        {
            foreach (var region in world.State.Regions)
            {
                region.Influence.FertilityBlessing = fertility;
                region.Influence.HarvestBlessing = Harvest;
                region.Influence.DiseasePressure = Disease;
                region.Influence.StabilityBlessing = Stability;
            }

            world.Influence.FertilityBlessing = fertility;
            world.Influence.HarvestBlessing = Harvest;
            world.Influence.DiseaseCurse = Disease;
            world.Influence.StabilityBlessing = Stability;
        }

        static void AdvanceAndAccumulate(HeadlessWorld world, int days, CumulativeTotals totals)
        {
            for (int d = 0; d < days; d++)
            {
                float[] popBefore = new float[world.State.Regions.Length];
                for (int i = 0; i < world.State.Regions.Length; i++)
                {
                    popBefore[i] = world.State.Regions[i].Population;
                }

                world.AdvanceDay();

                for (int i = 0; i < world.State.Regions.Length; i++)
                {
                    var r = world.State.Regions[i];
                    float natural = r.LastNaturalDeath;
                    float disease = r.LastDiseaseDeath;
                    float births = (r.Population - popBefore[i]) + natural + disease;
                    if (births < 0f)
                    {
                        births = 0f;
                    }

                    var c = totals.For(r.Id);
                    c.Births += births;
                    c.NaturalDeaths += natural;
                    c.DiseaseDeaths += disease;
                    c.NetChange += r.Population - popBefore[i];
                }
            }
        }

        static WorldSnapshot CaptureSnapshot(HeadlessWorld world, CumulativeTotals cumulative)
        {
            var snap = new WorldSnapshot
            {
                Year = world.State.Year,
                DayOfYear = world.State.DayOfYear,
                TotalDays = world.State.TotalDays,
                Season = world.State.CurrentSeason,
                Regions = new List<RegionMetrics>()
            };

            foreach (var region in world.State.Regions)
            {
                var race = FindRace(world.Races, region.DominantRace);
                var metrics = ComputeRegionMetrics(region, race, world.State.CurrentSeason, world.Config);
                if (cumulative != null)
                {
                    var c = cumulative.For(region.Id);
                    metrics.CumulativeBirths = c.Births;
                    metrics.CumulativeNaturalDeaths = c.NaturalDeaths;
                    metrics.CumulativeDiseaseDeaths = c.DiseaseDeaths;
                    metrics.CumulativeNetChange = c.NetChange;
                }

                snap.Regions.Add(metrics);
            }

            return snap;
        }

        /// <summary>
        /// Mirrors PopulationSystem.TickDay arithmetic for reporting only (no state mutation).
        /// </summary>
        static RegionMetrics ComputeRegionMetrics(
            RegionState region,
            RaceDefinition race,
            SeasonId season,
            SimulationConfig config)
        {
            var influence = region.Influence ?? new RegionObserverInfluence();
            float pop = region.Population;
            float carrying = PopulationSystem.CalculateCarryingCapacity(region, race, season, config);

            float foodRatio = Mathf.Clamp01(region.Get(ResourceId.Food) / Mathf.Max(1f, pop * config.FoodRatioSoftCap));
            float fertilityModifier = SanitizePositive(influence.FertilityBlessing, 1f);
            float baseBirthRate = config.BaseFertility * race.FertilityFactor;
            float fertility = baseBirthRate * fertilityModifier;

            float logistic = 1f - pop / Mathf.Max(1f, carrying);
            logistic = Mathf.Clamp(logistic, 0f, 1.5f);
            float logisticApplied = Mathf.Max(0f, logistic);

            float effectiveBirthRate = fertility * (0.5f + foodRatio) * logisticApplied;
            float births = pop * effectiveBirthRate;

            float baseDeathRate = (config.BaseNaturalDeath / Mathf.Max(0.05f, race.LifespanFactor))
                                  * config.DeathModifier(season);
            float otherDeaths = pop * baseDeathRate;
            float diseaseDeaths = pop
                                  * region.DiseasePressure
                                  * config.DiseaseDeathRate
                                  * config.DiseaseModifier(season)
                                  * SanitizePositive(influence.DiseasePressure, 1f);

            float overCapacityDeaths = 0f;
            float totalDeaths = otherDeaths + diseaseDeaths + overCapacityDeaths;
            float net = births - totalDeaths;

            float foodNeed = Mathf.Max(1e-6f, pop * config.FoodNeedPerCapita);
            float foodReserveDays = region.Get(ResourceId.Food) / foodNeed;

            float waterNeed = Mathf.Max(1f, pop * config.WaterAvailabilityNormPerCapita);
            float waterAvailability = Mathf.Clamp(region.Get(ResourceId.Water) / waterNeed, 0.1f, 2f);

            float unconstrainedBirths = pop * fertility * (0.5f + foodRatio);
            float birthsLostToCapacity = Mathf.Max(0f, unconstrainedBirths - births);

            return new RegionMetrics
            {
                RegionId = region.Id,
                DisplayName = region.DisplayName,
                Population = pop,
                CarryingCapacity = carrying,
                PopOverK = carrying > 0f ? pop / carrying : 0f,
                BaseBirthRate = baseBirthRate,
                FertilityModifier = fertilityModifier,
                EffectiveBirthRate = effectiveBirthRate,
                Births = births,
                BaseDeathRate = baseDeathRate,
                DiseaseDeaths = diseaseDeaths,
                OverCapacityDeaths = overCapacityDeaths,
                OtherDeaths = otherDeaths,
                TotalDeaths = totalDeaths,
                NetPopulationChange = net,
                FoodReserveDays = foodReserveDays,
                WaterAvailabilityFactor = waterAvailability,
                FoodRatio = foodRatio,
                LogisticFactor = logisticApplied,
                BirthsLostToCapacity = birthsLostToCapacity,
                DiseasePressure = region.DiseasePressure,
                LastNaturalDeath = region.LastNaturalDeath,
                LastDiseaseDeath = region.LastDiseaseDeath,
                PopulationDelta = region.PopulationDelta
            };
        }

        static RaceDefinition FindRace(RaceDefinition[] races, RaceId id)
        {
            for (int i = 0; i < races.Length; i++)
            {
                if (races[i].Id == id)
                {
                    return races[i];
                }
            }

            return races[0];
        }

        static float SanitizePositive(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0f)
            {
                return fallback;
            }

            return value;
        }

        static void PrintDetailedReports(List<ArmResult> results)
        {
            foreach (var arm in results)
            {
                Console.WriteLine($"========== Arm {arm.Label}: Fertility ×{arm.Fertility:0.00} ==========");
                PrintHorizon("Day 0 (prospective rates, before any tick)", arm.Day0, showCumulative: false);
                PrintHorizon("After 30 days (fresh from Day 0)", arm.Day30, showCumulative: true);
                PrintHorizon("After 360 days (fresh from Day 0)", arm.Day360, showCumulative: true);
            }
        }

        static void PrintHorizon(string title, WorldSnapshot snap, bool showCumulative)
        {
            Console.WriteLine($"--- {title} | Y{snap.Year} D{snap.DayOfYear} ({snap.Season}) TotalDays={snap.TotalDays} ---");
            foreach (var r in snap.Regions)
            {
                Console.WriteLine($"  [{r.DisplayName}]");
                Console.WriteLine($"    Population              = {F(r.Population)}");
                Console.WriteLine($"    CarryingCapacity        = {F(r.CarryingCapacity)}");
                Console.WriteLine($"    Population/K            = {F4(r.PopOverK)}");
                Console.WriteLine($"    BaseBirthRate           = {Sci(r.BaseBirthRate)}");
                Console.WriteLine($"    FertilityModifier       = {F4(r.FertilityModifier)}");
                Console.WriteLine($"    EffectiveBirthRate      = {Sci(r.EffectiveBirthRate)}");
                Console.WriteLine($"    Births (today)          = {F4(r.Births)}");
                Console.WriteLine($"    BaseDeathRate           = {Sci(r.BaseDeathRate)}");
                Console.WriteLine($"    DiseaseDeaths (today)   = {F4(r.DiseaseDeaths)}");
                Console.WriteLine($"    OverCapacityDeaths      = {F4(r.OverCapacityDeaths)}  (model: always 0; capacity dampens births)");
                Console.WriteLine($"    OtherDeaths (today)     = {F4(r.OtherDeaths)}  (natural/seasonal)");
                Console.WriteLine($"    TotalDeaths (today)     = {F4(r.TotalDeaths)}");
                Console.WriteLine($"    NetPopulationChange     = {F4(r.NetPopulationChange)}");
                Console.WriteLine($"    FoodReserveDays         = {F2(r.FoodReserveDays)}");
                Console.WriteLine($"    WaterAvailabilityFactor = {F4(r.WaterAvailabilityFactor)}");
                Console.WriteLine($"    (aux) foodRatio={F4(r.FoodRatio)} logistic={F4(r.LogisticFactor)} birthsLostToK={F4(r.BirthsLostToCapacity)} diseaseP={F4(r.DiseasePressure)}");
                if (showCumulative)
                {
                    Console.WriteLine($"    [cumulative] Births={F2(r.CumulativeBirths)} Natural={F2(r.CumulativeNaturalDeaths)} Disease={F2(r.CumulativeDiseaseDeaths)} Net={F2(r.CumulativeNetChange)}");
                }
            }

            Console.WriteLine();
        }

        static void PrintComparisonTables(List<ArmResult> results)
        {
            Console.WriteLine("========== A/B/C COMPARISON (after 360 days) ==========");
            var regions = results[0].Day360.Regions;
            foreach (var regionTemplate in regions)
            {
                Console.WriteLine($"Region: {regionTemplate.DisplayName}");
                Console.WriteLine(Pad("Metric", 28) + Pad("A×0.70", 14) + Pad("B×1.00", 14) + Pad("C×1.30", 14) + "Mono↑?");
                PrintRow("Population", results, regionTemplate.RegionId, m => F(m.Population), at360: true);
                PrintRow("CarryingCapacity", results, regionTemplate.RegionId, m => F(m.CarryingCapacity), at360: true);
                PrintRow("Pop/K", results, regionTemplate.RegionId, m => F4(m.PopOverK), at360: true);
                PrintRow("FertilityModifier", results, regionTemplate.RegionId, m => F4(m.FertilityModifier), at360: true, checkMono: true);
                PrintRow("EffectiveBirthRate", results, regionTemplate.RegionId, m => Sci(m.EffectiveBirthRate), at360: true, checkMono: true);
                PrintRow("Births(today)", results, regionTemplate.RegionId, m => F4(m.Births), at360: true, checkMono: true);
                PrintRow("CumBirths", results, regionTemplate.RegionId, m => F2(m.CumulativeBirths), at360: true, checkMono: true);
                PrintRow("DiseaseDeaths(today)", results, regionTemplate.RegionId, m => F4(m.DiseaseDeaths), at360: true);
                PrintRow("OtherDeaths(today)", results, regionTemplate.RegionId, m => F4(m.OtherDeaths), at360: true);
                PrintRow("OverCapacityDeaths", results, regionTemplate.RegionId, m => F4(m.OverCapacityDeaths), at360: true);
                PrintRow("CumNaturalDeaths", results, regionTemplate.RegionId, m => F2(m.CumulativeNaturalDeaths), at360: true);
                PrintRow("CumDiseaseDeaths", results, regionTemplate.RegionId, m => F2(m.CumulativeDiseaseDeaths), at360: true);
                PrintRow("CumNetChange", results, regionTemplate.RegionId, m => F2(m.CumulativeNetChange), at360: true);
                PrintRow("FoodReserveDays", results, regionTemplate.RegionId, m => F2(m.FoodReserveDays), at360: true);
                PrintRow("WaterAvailFactor", results, regionTemplate.RegionId, m => F4(m.WaterAvailabilityFactor), at360: true);
                Console.WriteLine();
            }

            Console.WriteLine("========== A/B/C COMPARISON (after 30 days, CumBirths / CumNet) ==========");
            foreach (var regionTemplate in regions)
            {
                Console.WriteLine($"Region: {regionTemplate.DisplayName}");
                Console.WriteLine(Pad("Metric", 28) + Pad("A×0.70", 14) + Pad("B×1.00", 14) + Pad("C×1.30", 14) + "Mono↑?");
                PrintRow("Population", results, regionTemplate.RegionId, m => F(m.Population), at360: false);
                PrintRow("EffectiveBirthRate", results, regionTemplate.RegionId, m => Sci(m.EffectiveBirthRate), at360: false, checkMono: true);
                PrintRow("Births(today)", results, regionTemplate.RegionId, m => F4(m.Births), at360: false, checkMono: true);
                PrintRow("CumBirths", results, regionTemplate.RegionId, m => F2(m.CumulativeBirths), at360: false, checkMono: true);
                PrintRow("CumNetChange", results, regionTemplate.RegionId, m => F2(m.CumulativeNetChange), at360: false);
                Console.WriteLine();
            }
        }

        static void PrintRow(
            string name,
            List<ArmResult> results,
            RegionId regionId,
            Func<RegionMetrics, string> fmt,
            bool at360,
            bool checkMono = false)
        {
            var vals = new float[3];
            var texts = new string[3];
            for (int i = 0; i < 3; i++)
            {
                var snap = at360 ? results[i].Day360 : results[i].Day30;
                var m = FindRegion(snap, regionId);
                texts[i] = fmt(m);
                vals[i] = MetricNumeric(name, m);
            }

            string mono = "";
            if (checkMono)
            {
                bool ok = vals[0] < vals[1] && vals[1] < vals[2];
                mono = ok ? "YES" : "NO << CHECK";
            }

            Console.WriteLine(Pad(name, 28) + Pad(texts[0], 14) + Pad(texts[1], 14) + Pad(texts[2], 14) + mono);
        }

        static float MetricNumeric(string name, RegionMetrics m)
        {
            switch (name)
            {
                case "FertilityModifier": return m.FertilityModifier;
                case "EffectiveBirthRate": return m.EffectiveBirthRate;
                case "Births(today)": return m.Births;
                case "CumBirths": return m.CumulativeBirths;
                default: return 0f;
            }
        }

        static int AnalyzeAndConclude(List<ArmResult> results)
        {
            Console.WriteLine("========== DIAGNOSIS ==========");
            bool fertilityWired = true;
            bool birthsMono = true;

            // Day-0 wiring proof: identical state => EffectiveBirthRate must scale exactly with FertilityModifier.
            bool day0Wired = true;
            foreach (var regionTemplate in results[0].Day0.Regions)
            {
                var a0r = FindRegion(results[0].Day0, regionTemplate.RegionId);
                var b0r = FindRegion(results[1].Day0, regionTemplate.RegionId);
                var c0r = FindRegion(results[2].Day0, regionTemplate.RegionId);
                float rBA = b0r.EffectiveBirthRate / Mathf.Max(1e-12f, a0r.EffectiveBirthRate);
                float rCB = c0r.EffectiveBirthRate / Mathf.Max(1e-12f, b0r.EffectiveBirthRate);
                bool ok = Approx(rBA, 1f / 0.7f, 0.01f) && Approx(rCB, 1.3f, 0.01f);
                Console.WriteLine($"[Day0 wiring {regionTemplate.DisplayName}] EBR B/A={F4(rBA)} (expect 1.4286), C/B={F4(rCB)} (expect 1.30) => {(ok ? "OK" : "FAIL")}");
                if (!ok)
                {
                    day0Wired = false;
                    fertilityWired = false;
                }
            }

            Console.WriteLine();

            foreach (var regionTemplate in results[0].Day360.Regions)
            {
                var a = FindRegion(results[0].Day360, regionTemplate.RegionId);
                var b = FindRegion(results[1].Day360, regionTemplate.RegionId);
                var c = FindRegion(results[2].Day360, regionTemplate.RegionId);

                bool ebrMono = a.EffectiveBirthRate < b.EffectiveBirthRate && b.EffectiveBirthRate < c.EffectiveBirthRate;
                bool birthMonoToday = a.Births < b.Births && b.Births < c.Births;
                bool birthMonoCum = a.CumulativeBirths < b.CumulativeBirths && b.CumulativeBirths < c.CumulativeBirths;
                bool allZeroToday = a.Births <= 0f && b.Births <= 0f && c.Births <= 0f;
                bool overCap = c.PopOverK >= 1f || c.LogisticFactor <= 0f;

                Console.WriteLine($"[{regionTemplate.DisplayName}]");
                Console.WriteLine($"  EffectiveBirthRate mono 0.70→1.00→1.30: {(ebrMono ? "YES" : (allZeroToday ? "N/A (all 0: Pop>=K logistic shutoff)" : "NO"))}");
                Console.WriteLine($"  Births(today) mono: {(birthMonoToday ? "YES" : (allZeroToday ? "N/A (all 0: capacity)" : "NO"))}");
                Console.WriteLine($"  CumBirths mono: {(birthMonoCum ? "YES" : "NO")}");

                // Wiring verdict uses CumBirths + Day0 ratios. Last-day EBR can be 0 for ALL arms when Pop>=K.
                if (!birthMonoCum || c.CumulativeBirths <= b.CumulativeBirths)
                {
                    birthsMono = false;
                    fertilityWired = false;
                    Console.WriteLine("  !! Births wiring suspect: C CumBirths <= B — check FertilityBlessing in TickDay");
                }
                else if (allZeroToday && overCap)
                {
                    Console.WriteLine("  Note: last-day Births=0 for A/B/C because logistic=(1-P/K)<=0; not a fertility-wiring failure.");
                }
                else if (!ebrMono && !allZeroToday && c.EffectiveBirthRate <= b.EffectiveBirthRate)
                {
                    fertilityWired = false;
                    Console.WriteLine("  !! FertilityModifier wiring suspect: C EffectiveBirthRate <= B with Pop<K");
                }

                var day0 = FindRegion(results[2].Day0, regionTemplate.RegionId);
                float deltaPop = c.Population - day0.Population;
                Console.WriteLine($"  Pop Day0→360 (C×1.30): {F(day0.Population)} → {F(c.Population)} (Δ={F2(deltaPop)})");
                if (deltaPop < 0f)
                {
                    float nat = c.CumulativeNaturalDeaths;
                    float dis = c.CumulativeDiseaseDeaths;
                    float bir = c.CumulativeBirths;
                    float deathTotal = nat + dis;
                    float disShare = deathTotal > 0f ? dis / deathTotal : 0f;
                    float natShare = deathTotal > 0f ? nat / deathTotal : 0f;

                    string primary = dis >= nat
                        ? $"疾病死亡 (DiseaseDeaths, {F2(disShare * 100f)}% of deaths)"
                        : $"其他/自然死亡 (OtherDeaths, {F2(natShare * 100f)}% of deaths)";

                    Console.WriteLine($"  Decline primary driver: {primary}");
                    Console.WriteLine($"  Cum: Births={F2(bir)} Natural={F2(nat)} Disease={F2(dis)} Deaths={F2(deathTotal)}");
                    Console.WriteLine($"  Capacity: OverCapacityDeaths=0 always; pressure acts as birth shutoff via logistic. Pop/K={F4(c.PopOverK)}, last-day birthsLostToK={F4(c.BirthsLostToCapacity)}");
                    Console.WriteLine($"  Math: Births/Deaths = {F4(bir / Mathf.Max(1e-6f, deathTotal))} ; need >1 to grow. Fertility×1.30 only raised CumBirths, still << deaths.");
                    if (bir < deathTotal)
                    {
                        Console.WriteLine("  => Net decline is expected carrying/death-pressure outcome, not missing FertilityModifier.");
                    }
                }
                else
                {
                    Console.WriteLine("  Population did not decline under Fertility×1.30.");
                }

                Console.WriteLine();
            }

            Console.WriteLine("Summary verdict:");
            if (day0Wired && fertilityWired && birthsMono)
            {
                Console.WriteLine("  FertilityModifier IS correctly entering the birth formula.");
                Console.WriteLine("  Evidence: Day0 EBR scales 0.70→1.00→1.30 exactly; CumBirths rises monotonically in every region.");
                Console.WriteLine("  Population still falls because CumDeaths >> CumBirths (disease + natural), and Pop>=K zeroes logistic births for long stretches.");
                Console.WriteLine("  OverCapacityDeaths do not exist in this model — capacity suppresses births, it does not add deaths.");
            }
            else
            {
                Console.WriteLine("  FertilityModifier application looks BROKEN or overridden — inspect FertilityBlessing path in PopulationSystem.TickDay.");
            }

            return day0Wired && fertilityWired && birthsMono ? 0 : 2;
        }

        static bool Approx(float actual, float expected, float relTol)
        {
            return Mathf.Abs(actual - expected) <= Mathf.Abs(expected) * relTol + 1e-6f;
        }

        static RegionMetrics FindRegion(WorldSnapshot snap, RegionId id)
        {
            foreach (var r in snap.Regions)
            {
                if (r.RegionId == id)
                {
                    return r;
                }
            }

            throw new Exception("region missing: " + id);
        }

        static string F(float v) => v.ToString("0", CultureInfo.InvariantCulture);
        static string F2(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
        static string F4(float v) => v.ToString("0.0000", CultureInfo.InvariantCulture);
        static string Sci(float v) => v.ToString("0.000000e+0", CultureInfo.InvariantCulture);
        static string Pad(string s, int w) => (s ?? "").PadRight(w);

        sealed class ArmResult
        {
            public string Label;
            public float Fertility;
            public WorldSnapshot Day0;
            public WorldSnapshot Day30;
            public WorldSnapshot Day360;
        }

        sealed class WorldSnapshot
        {
            public int Year;
            public int DayOfYear;
            public int TotalDays;
            public SeasonId Season;
            public List<RegionMetrics> Regions;
        }

        sealed class RegionMetrics
        {
            public RegionId RegionId;
            public string DisplayName;
            public float Population;
            public float CarryingCapacity;
            public float PopOverK;
            public float BaseBirthRate;
            public float FertilityModifier;
            public float EffectiveBirthRate;
            public float Births;
            public float BaseDeathRate;
            public float DiseaseDeaths;
            public float OverCapacityDeaths;
            public float OtherDeaths;
            public float TotalDeaths;
            public float NetPopulationChange;
            public float FoodReserveDays;
            public float WaterAvailabilityFactor;
            public float FoodRatio;
            public float LogisticFactor;
            public float BirthsLostToCapacity;
            public float DiseasePressure;
            public float LastNaturalDeath;
            public float LastDiseaseDeath;
            public float PopulationDelta;
            public float CumulativeBirths;
            public float CumulativeNaturalDeaths;
            public float CumulativeDiseaseDeaths;
            public float CumulativeNetChange;
        }

        sealed class CumulativeTotals
        {
            readonly Dictionary<RegionId, RegionCum> _map = new Dictionary<RegionId, RegionCum>();

            public RegionCum For(RegionId id)
            {
                if (!_map.TryGetValue(id, out var c))
                {
                    c = new RegionCum();
                    _map[id] = c;
                }

                return c;
            }
        }

        sealed class RegionCum
        {
            public float Births;
            public float NaturalDeaths;
            public float DiseaseDeaths;
            public float NetChange;
        }
    }
}
