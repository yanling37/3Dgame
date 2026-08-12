using System;
using DivineWorld.Simulation.Core;
using DivineWorld.Simulation.Data;
using DivineWorld.Simulation.Player;
using DivineWorld.Simulation.Systems;

namespace HeadlessSimTests
{
    /// <summary>
    /// MonoBehaviour-free world runner for headless P2-A validation.
    /// Mirrors SimulationWorld.AdvanceDay pipeline.
    /// </summary>
    public sealed class HeadlessWorld
    {
        public WorldState State { get; private set; }
        public RaceDefinition[] Races { get; private set; }
        public SimulationConfig Config { get; private set; }
        public ObserverInfluence Influence { get; private set; }
        public System.Random Rng { get; private set; }

        public HeadlessWorld(int seed = 20260810)
        {
            Reset(seed);
        }

        public void Reset(int seed = 20260810)
        {
            Rng = new System.Random(seed);
            Config = SimulationConfig.CreateDefault();
            Races = DefaultWorldFactory.CreateRaces();
            State = DefaultWorldFactory.CreateWorld();
            Influence = new ObserverInfluence();
            Influence.Bind(State);
        }

        public void AdvanceDay()
        {
            DailySimulation.SimulateDay(State, Races, Config, Rng);
            SeasonSystem.AdvanceCalendar(State);
        }

        public void AdvanceDays(int days)
        {
            for (int i = 0; i < days; i++)
            {
                AdvanceDay();
            }
        }

        public RegionState Region(RegionId id) => RegionLookup.FindRegion(State.Regions, id);
    }
}
