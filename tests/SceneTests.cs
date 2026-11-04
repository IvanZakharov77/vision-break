using System;
using System.IO;

internal static class SceneTests
{
    private static void Check(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
    }

    public static void Run()
    {
        Check(SceneMotion.Lighting(0, false) == 0, "Start at dawn");
        Check(Math.Abs(SceneMotion.Lighting(270000, false) - 0.5) < 0.000001, "Blend before the day boundary");
        Check(SceneMotion.Lighting(300000, false) == 1, "Day begins after five minutes");
        Check(SceneMotion.Lighting(600000, false) == 2, "Evening begins after ten minutes");
        Check(SceneMotion.Lighting(900000, false) == 3, "Night begins after fifteen minutes");
        Check(SceneMotion.Lighting(1200000, false) == 3, "Hold night through the end");
        Check(SceneMotion.Lighting(5000, true) == 1 && SceneMotion.Lighting(15000, true) == 3, "Demo phases last five seconds");
        Check(SceneMotion.Position(0) == 0 && SceneMotion.Position(8000) == 1, "Reach both edges");
        Check(SceneMotion.Position(12000) == 0.5 && SceneMotion.Position(16000) == 0, "Walk back to the left");
        Check(SceneMotion.FacingRight(7999) && !SceneMotion.FacingRight(8000) && SceneMotion.FacingRight(16000), "Turn at each edge");

        string settings = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        try
        {
            for (int i = 0; i < 6; i++)
                Check(SceneMotion.TakeSeason(settings) == i % 4, "Persist the next season across instances");
            File.WriteAllText(settings, "99");
            Check(SceneMotion.TakeSeason(settings) == 0, "Recover from invalid season settings");
        }
        finally { File.Delete(settings); }
        Console.WriteLine("Scene timing, direction and season persistence checks passed.");
    }
}
