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
                case "observation":
                    {
                        int v03 = Phase2BObservationTests.Run();
                        Console.WriteLine();
                        int v04 = Phase2BHistoryTests.Run();
                        return v03 != 0 ? v03 : v04;
                    }
                case "p2b-v03":
                    return Phase2BObservationTests.Run();
                case "p2b-v04":
                case "history":
                    return Phase2BHistoryTests.Run();
                default:
                    Console.WriteLine("Divine World P2-A Headless Tests");
                    Console.WriteLine("================================");
                    int p2a = Phase2ATests.RunAll();
                    Console.WriteLine();
                    int fert = FertilityModifierDiagnostic.Run();
                    Console.WriteLine();
                    int p2a2 = Phase2A2Tests.Run();
                    Console.WriteLine();
                    int p2b = Phase2BObservationTests.Run();
                    Console.WriteLine();
                    int p2b04 = Phase2BHistoryTests.Run();
                    return p2a != 0 ? p2a : (fert != 0 ? fert : (p2a2 != 0 ? p2a2 : (p2b != 0 ? p2b : p2b04)));
            }
        }
    }
}
