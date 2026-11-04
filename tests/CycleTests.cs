using System;

internal static class CycleTests
{
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
    }

    public static void Main()
    {
        var cycle = new BreakCycle(0, false);
        Check(!cycle.OnBreak && cycle.RemainingSeconds(0) == 2400, "Start with 40 minutes of use");
        Check(!cycle.Advance(2399999), "Do not start break before deadline");
        Check(cycle.Advance(2400000) && cycle.OnBreak, "Enter break exactly at deadline");
        Check(cycle.RemainingSeconds(2400000) == 1200, "Give 20 minutes for the break");
        Check(!cycle.Advance(2400001), "Do not dispatch a second close request");
        Check(cycle.RemainingSeconds(3599999) == 1, "Round the last partial second up");
        Check(cycle.Advance(3600000) && !cycle.OnBreak, "End break exactly at zero");
        Check(cycle.RemainingSeconds(3600000) == 2400, "Restart the use cycle");
        Check(cycle.Advance(9000000) && cycle.OnBreak, "A late callback starts a fresh break");
        Check(cycle.RemainingSeconds(9000000) == 1200, "A late callback still gives a full break");
        var demo = new BreakCycle(500, true);
        Check(demo.OnBreak && demo.RemainingSeconds(500) == 20, "Demo starts with a short break");
        Check(demo.Advance(20500) && !demo.OnBreak, "Demo ends after twenty seconds");
        Console.WriteLine("12 cycle checks passed.");
        ReleaseTests.Run();
    }
}
