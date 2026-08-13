using System;
using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using DivineWorld.Simulation.Systems;

namespace HeadlessSimTests
{
    /// <summary>
    /// MonoBehaviour-free world runner for headless P2-A / P2-B validation.
    /// Mirrors SimulationWorld reset / day / FastForward lifecycle events (no math changes).
    /// </summary>
    public sealed class HeadlessWorld
    {
        public WorldState State { get; private set; }
        public RaceDefinition[] Races { get; private set; }
        public SimulationConfig Config { get; private set; }
        public ObserverInfluence Influence { get; private set; }
        public System.Random Rng { get; private set; }
        public int Seed { get; private set; }

        public event Action<WorldState> OnDayAdvanced;
        public event Action<WorldState> OnWorldReset;

        public HeadlessWorld(int seed = 20260810)
        {
            Reset(seed);
        }

        public void Reset(int seed = 20260810)
        {
            Seed = seed;
            Rng = new System.Random(seed);
            Config = SimulationConfig.CreateDefault();
            Races = DefaultWorldFactory.CreateRaces();
            State = DefaultWorldFactory.CreateWorld();
            State.RandomSeed = seed;
            Influence = new ObserverInfluence();
            Influence.Bind(State);
            OnWorldReset?.Invoke(State);
            OnDayAdvanced?.Invoke(State);
        }

        public void AdvanceDay()
        {
            DailySimulation.SimulateDay(State, Races, Config, Rng);
            OnDayAdvanced?.Invoke(State);
        }

        public void AdvanceDays(int days)
        {
            for (int i = 0; i < days; i++)
            {
                AdvanceDay();
                if (State.HaltedOnNumericError)
                {
                    break;
                }
            }
        }

        public FastForwardSystem.Result FastForwardDays(int days)
        {
            return FastForwardSystem.FastForwardToTotalDay(State, Races, Config, State.TotalDays + days);
        }

        /// <summary>
        /// Same contract as SimulationWorld.FastForwardYears: replace State, then raise OnDayAdvanced.
        /// </summary>
        public FastForwardSystem.Result FastForwardYears(int years)
        {
            var result = FastForwardSystem.FastForwardYears(State, Races, Config, years);
            State = result.State;
            Influence.Bind(State);
            SeasonSystem.SyncFromCalendar(State);
            OnDayAdvanced?.Invoke(State);
            return result;
        }

        public RegionState Region(RegionId id) => RegionLookup.FindRegion(State.Regions, id);

        public void ApplyGlobalInfluence(float fertility, float harvest, float disease, float stability)
        {
            foreach (var region in State.Regions)
            {
                region.Influence.FertilityBlessing = fertility;
                region.Influence.HarvestBlessing = harvest;
                region.Influence.DiseasePressure = disease;
                region.Influence.StabilityBlessing = stability;
            }

            Influence.FertilityBlessing = fertility;
            Influence.HarvestBlessing = harvest;
            Influence.DiseaseCurse = disease;
            Influence.StabilityBlessing = stability;
        }
    }
}
