using System;
using System.Globalization;
using System.IO;
using System.Text;

internal sealed class CycleSnapshot
{
    public bool OnBreak;
    public long Time;
    public int Season;
    public bool Sound;
}

internal sealed class PersistentCycle
{
    private const long UseLength = 2400000;
    public const long BreakLength = 1200000;
    private long deadline;

    public PersistentCycle(CycleSnapshot saved, long utc, long elapsed)
    {
        OnBreak = saved != null && saved.OnBreak;
        Season = saved == null ? 0 : saved.Season;
        Sound = saved == null || saved.Sound;
        deadline = OnBreak ? elapsed + saved.Time :
            saved == null ? utc + UseLength : Math.Min(saved.Time, utc + UseLength);
    }

    public bool OnBreak { get; private set; }
    public int Season { get; set; }
    public bool Sound { get; set; }

    public long Remaining(long utc, long elapsed)
    {
        return Math.Max(0, deadline - (OnBreak ? elapsed : utc));
    }

    public bool Advance(long utc, long elapsed)
    {
        if (Remaining(utc, elapsed) > 0) return false;
        OnBreak = !OnBreak;
        deadline = OnBreak ? elapsed + BreakLength : utc + UseLength;
        return true;
    }

    public CycleSnapshot Snapshot(long utc, long elapsed)
    {
        return new CycleSnapshot
        {
            OnBreak = OnBreak,
            Time = OnBreak ? Math.Max(1, Remaining(utc, elapsed)) : deadline,
            Season = Season,
            Sound = Sound
        };
    }
}

internal sealed class CycleStore
{
    private readonly string path;

    public CycleStore(string folder)
    {
        path = Path.Combine(folder, "cycle.txt");
    }

    public CycleSnapshot Load()
    {
        return Read(path) ?? Read(path + ".bak");
    }

    private static CycleSnapshot Read(string file)
    {
        try
        {
            string[] fields = File.ReadAllText(file).Split('|');
            long time;
            int season;
            bool sound;
            if (fields.Length != 5 || fields[0] != "1" ||
                (fields[1] != "break" && fields[1] != "use") ||
                !long.TryParse(fields[2], NumberStyles.None, CultureInfo.InvariantCulture, out time) ||
                time < 1 || (fields[1] == "break" && time > PersistentCycle.BreakLength) ||
                !int.TryParse(fields[3], out season) || season < 0 || season > 3 ||
                !bool.TryParse(fields[4], out sound))
                return null;
            return new CycleSnapshot { OnBreak = fields[1] == "break", Time = time, Season = season, Sound = sound };
        }
        catch (IOException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    public bool Save(CycleSnapshot state)
    {
        string temporary = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string record = string.Join("|", "1", state.OnBreak ? "break" : "use",
                state.Time.ToString(CultureInfo.InvariantCulture),
                state.Season.ToString(CultureInfo.InvariantCulture), state.Sound.ToString());
            byte[] bytes = Encoding.ASCII.GetBytes(record);
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path))
                File.Replace(temporary, path, path + ".bak", true);
            else
                File.Move(temporary, path);
            return true;
        }
        catch (IOException) { return false; }
        catch (UnauthorizedAccessException) { return false; }
    }
}
