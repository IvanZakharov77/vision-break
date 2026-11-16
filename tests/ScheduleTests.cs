using System;

internal static class ScheduleTests
{
    public static void Run()
    {
        DateTime monday = new DateTime(2026, 11, 16);
        Check(!QuietHours.IsActive(monday.AddHours(19).AddTicks(-1)), "Before Monday 19:00");
        Check(QuietHours.IsActive(monday.AddHours(19)), "Monday 19:00");
        Check(QuietHours.IsActive(monday.AddHours(21).AddTicks(-1)), "Before Monday 21:00");
        Check(!QuietHours.IsActive(monday.AddHours(21)), "Monday 21:00");
        Check(QuietHours.IsActive(monday.AddDays(4).AddHours(20)), "Friday evening");
        Check(!QuietHours.IsActive(monday.AddDays(1).AddHours(20)), "Tuesday evening");
        Check(!QuietHours.IsActive(monday.AddDays(6).AddHours(20)), "Sunday evening");
        var cycle = new PersistentCycle(new CycleSnapshot { OnBreak = true, Time = 900000, Sound = false, Season = 2 }, 0, 0);
        cycle.RestartUseAt(100000);
        Check(!cycle.OnBreak && cycle.Remaining(100000, 0) == 2400000, "Restart use after quiet hours");
        Check(cycle.Remaining(700000, 0) == 1800000, "New deadline survives elapsed quiet time");
        Check(!cycle.Sound && cycle.Season == 2, "Keep sound and season");
        Console.WriteLine("Quiet hours boundaries and cycle reset checks passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
