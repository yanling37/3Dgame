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
                default:
                    Console.WriteLine("Divine World P2-A Headless Tests");
                    Console.WriteLine("================================");
                    int p2a = Phase2ATests.RunAll();
                    Console.WriteLine();
                    int fert = FertilityModifierDiagnostic.Run();
                    Console.WriteLine();
                    int p2a2 = Phase2A2Tests.Run();
                    return p2a != 0 ? p2a : (fert != 0 ? fert : p2a2);
            }
        }
    }
}
