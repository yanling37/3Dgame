using System;

namespace HeadlessSimTests
{
    internal static class Program
    {
        static int Main(string[] args)
        {
            Console.WriteLine("Divine World P2-A Headless Tests");
            Console.WriteLine("================================");
            return Phase2ATests.RunAll();
        }
    }
}
