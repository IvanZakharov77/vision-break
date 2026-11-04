using System;
using System.Globalization;
using System.IO;

internal static class SceneMotion
{
    public static double Lighting(long elapsed, bool demo)
    {
        int phaseLength = demo ? 5000 : 300000;
        int phase = (int)Math.Min(3, elapsed / phaseLength);
        if (phase == 3) return 3;
        double within = (elapsed % phaseLength) / (double)phaseLength;
        double blend = Math.Max(0, (within - 0.8) * 5);
        // Smoothstep eases the fade in and out near the phase boundary.
        return phase + blend * blend * (3 - 2 * blend);
    }

    public static double Position(long elapsed)
    {
        double passage = elapsed % 16000 / 8000.0;
        return passage <= 1 ? passage : 2 - passage;
    }

    public static bool FacingRight(long elapsed)
    {
        return elapsed % 16000 < 8000;
    }

    public static int TakeSeason(string path)
    {
        int season = 0;
        try
        {
            int saved;
            if (File.Exists(path) && int.TryParse(File.ReadAllText(path), out saved) && saved >= 0 && saved < 4)
                season = saved;
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, ((season + 1) % 4).ToString(CultureInfo.InvariantCulture));
            if (File.Exists(path))
                File.Replace(temporary, path, null);
            else
                File.Move(temporary, path);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return season;
    }
}
