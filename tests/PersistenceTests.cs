using System;
using System.IO;

internal static class PersistenceTests
{
    public static void Run()
    {
        const long start = 1700000000000;
        var cycle = new PersistentCycle(null, start, 0);
        var resumed = new PersistentCycle(cycle.Snapshot(start + 1200000, 1200000), start + 1800000, 0);
        Check(resumed.Remaining(start + 1800000, 0) == 600000, "Shutdown time counts toward normal use");
        Check(resumed.Advance(start + 2400000, 600000) && resumed.OnBreak, "Use deadline triggers break");
        var overdue = new PersistentCycle(cycle.Snapshot(start, 0), start + 7200000, 0);
        Check(overdue.Advance(start + 7200000, 0) && overdue.Remaining(start + 7200000, 0) == 1200000,
            "An overdue login starts a full break");

        resumed.Season = 2;
        resumed.Sound = false;
        var saved = resumed.Snapshot(start + 2700000, 900000);
        var breakResume = new PersistentCycle(saved, start + 7000000, 0);
        Check(breakResume.OnBreak && breakResume.Remaining(start + 7000000, 0) == 900000, "Offline time does not count during a break");
        Check(breakResume.Season == 2 && !breakResume.Sound, "Restore scene and sound");
        Check(breakResume.Advance(start + 7900000, 900000) && !breakResume.OnBreak, "Resumed break ends normally");

        string folder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            var store = new CycleStore(folder);
            Check(store.Save(saved) && store.Save(breakResume.Snapshot(start + 7900000, 900000)), "Save both phases");
            Check(!store.Load().OnBreak, "Read latest complete checkpoint");
            File.WriteAllText(Path.Combine(folder, "cycle.txt"), "invalid");
            Check(store.Load().OnBreak, "Recover last valid checkpoint from backup");
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
        Console.WriteLine("Persistent cycle checks passed.");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
    }
}
