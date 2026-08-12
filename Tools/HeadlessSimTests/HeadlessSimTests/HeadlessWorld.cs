using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using DivineWorld.Simulation.Systems;

namespace HeadlessSimTests
{
    /// <summary>
    /// MonoBehaviour-free world runner for headless P2-A validation.
    /// </summary>
    public sealed class HeadlessWorld
    {
        public WorldState State { get; private set; }
        public RaceDefinition[] Races { get; private set; }
        public SimulationConfig Config { get; private set; }
        public ObserverInfluence Influence { get; private set; }
        public System.Random Rng { get; private set; }
        public int Seed { get; private set; }

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
        }

        public void AdvanceDay()
        {
            DailySimulation.SimulateDay(State, Races, Config, Rng);
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
