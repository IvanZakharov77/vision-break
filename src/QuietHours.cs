using System;

internal static class QuietHours
{
    public static bool IsActive(DateTime local)
    {
        return (local.DayOfWeek == DayOfWeek.Monday || local.DayOfWeek == DayOfWeek.Friday) &&
            local.TimeOfDay >= TimeSpan.FromHours(19) && local.TimeOfDay < TimeSpan.FromHours(21);
    }

    public static long EndUtcMilliseconds(DateTime local)
    {
        return DateTime.SpecifyKind(local.Date.AddHours(21), DateTimeKind.Local)
            .ToUniversalTime().Ticks / TimeSpan.TicksPerMillisecond;
    }
}
