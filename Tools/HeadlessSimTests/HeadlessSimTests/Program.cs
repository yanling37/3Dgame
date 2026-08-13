using System;

namespace HeadlessSimTests
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            string mode = args != null && args.Length > 0 ? args[0].Trim().ToLowerInvariant() : "all";
            switch (mode)
            {
                case "fertility":
                case "pop-fertility":
                case "fertility-modifier":
                    return FertilityModifierDiagnostic.Run();
                case "p2a":
                case "phase2a":
                    Console.WriteLine("Divine World P2-A Headless Tests");
                    Console.WriteLine("================================");
                    return Phase2ATests.RunAll();
                case "p2a2":
                case "phase2a2":
                    return Phase2A2Tests.Run();
                case "p2b":
                case "p2b1":
                    Console.WriteLine("Divine World P2-B Observation Tests");
                    Console.WriteLine("==================================");
                    int phase1 = P2BObservationPhase1Tests.Run();
                    Console.WriteLine();
                    int v02 = ObservationTests.Run();
                    return phase1 != 0 ? phase1 : v02;
                case "observation":
                case "p2b-smoke":
                    Console.WriteLine("Divine World P2-B Observation Tests");
                    Console.WriteLine("==================================");
                    return ObservationTests.Run();
                default:
                    Console.WriteLine("Divine World P2-A Headless Tests");
                    Console.WriteLine("================================");
                    int p2a = Phase2ATests.RunAll();
                    Console.WriteLine();
                    int fert = FertilityModifierDiagnostic.Run();
                    Console.WriteLine();
                    int p2a2 = Phase2A2Tests.Run();
                    Console.WriteLine();
                    int p2b = ObservationTests.Run();
                    return p2a != 0 ? p2a : (fert != 0 ? fert : (p2a2 != 0 ? p2a2 : p2b));
            }
        }
    }
}
