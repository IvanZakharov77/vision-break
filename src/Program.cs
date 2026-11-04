using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

internal static class Program
{
    private const string InstanceName = @"Local\VisionBreak";
    private const string StopName = @"Local\VisionBreakStop";
    private const string UpdateStopName = @"Local\VisionBreakUpdateStop";

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 3 && args[0] == "--apply-update")
        {
            AutoUpdate.Apply(args[1], args[2]);
            return;
        }

        if (args.Length == 1 && (args[0] == "--stop" || args[0] == "--stop-for-update"))
        {
            Environment.ExitCode = StopRunningInstance(args[0] == "--stop-for-update");
            return;
        }

        bool demo = args.Length == 1 && args[0] == "--demo";
        bool first;
        using (var instance = new Mutex(true, demo ? @"Local\VisionBreakDemo" : InstanceName, out first))
        {
            if (!first) return;
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var stop = demo ? null : new EventWaitHandle(false, EventResetMode.AutoReset, StopName))
                using (var updateStop = demo ? null : new EventWaitHandle(false, EventResetMode.AutoReset, UpdateStopName))
                using (var session = new BreakSession(demo, stop, updateStop))
                    Application.Run(session);
            }
            finally
            {
                instance.ReleaseMutex();
            }
        }
    }

    private static int StopRunningInstance(bool update)
    {
        try
        {
            using (var instance = Mutex.OpenExisting(InstanceName))
            {
                try
                {
                    using (var signal = EventWaitHandle.OpenExisting(update ? UpdateStopName : StopName))
                        signal.Set();
                }
                catch (WaitHandleCannotBeOpenedException) { }

                try
                {
                    if (!instance.WaitOne(5000)) return 1;
                }
                catch (AbandonedMutexException) { }
                instance.ReleaseMutex();
            }
        }
        catch (WaitHandleCannotBeOpenedException) { }
        return 0;
    }
}

internal sealed class BreakSession : ApplicationContext
{
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly BreakCycle demoCycle;
    private readonly PersistentCycle cycle;
    private readonly CycleStore store;
    private readonly SessionWindow sessionWindow;
    private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 50 };
    private readonly EventWaitHandle stop;
    private readonly EventWaitHandle updateStop;
    private Task<UpdateOffer> updateCheck;
    private BreakScreen screen;
    private CycleSnapshot suspended;
    private long nextCheckpoint;
    private long lastBreakEnd = -1;

    public BreakSession(bool demo, EventWaitHandle stop, EventWaitHandle updateStop)
    {
        this.stop = stop;
        this.updateStop = updateStop;
        long elapsed = clock.ElapsedMilliseconds;
        if (demo)
        {
            demoCycle = new BreakCycle(elapsed, true);
            ShowBreak(true, 0, true, demoCycle.RemainingSeconds(elapsed), 0);
        }
        else
        {
            store = new CycleStore(AutoUpdate.Profile);
            long utc = UtcMilliseconds;
            cycle = new PersistentCycle(store.Load(), utc, elapsed);
            if (cycle.Advance(utc, elapsed))
                BeginBreak(utc, elapsed);
            else
            {
                Checkpoint(utc, elapsed);
                if (cycle.OnBreak) ShowCurrentBreak(utc, elapsed);
            }
            sessionWindow = new SessionWindow(SaveBeforeShutdown, Suspend, Resume);
            if (AutoUpdate.IsInstalled)
                updateCheck = Task.Run(() => AutoUpdate.StageAsync());
        }

        timer.Tick += OnTick;
        timer.Start();
    }

    private static long UtcMilliseconds { get { return DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond; } }

    private void OnTick(object sender, EventArgs e)
    {
        long elapsed = clock.ElapsedMilliseconds;
        if (demoCycle != null)
        {
            if (demoCycle.Advance(elapsed)) { CloseScreen(); ExitThread(); }
            else screen.ShowFrame(demoCycle.RemainingSeconds(elapsed), 20000 - demoCycle.RemainingMilliseconds(elapsed));
            return;
        }

        long utc = UtcMilliseconds;
        if (stop.WaitOne(0))
        {
            SaveBeforeShutdown();
            ExitThread();
            return;
        }
        if (suspended != null) return;
        if (updateStop.WaitOne(0) && !cycle.OnBreak && lastBreakEnd >= 0 &&
            elapsed - lastBreakEnd < 120000 && cycle.Remaining(utc, elapsed) > 2220000 &&
            Checkpoint(utc, elapsed))
        {
            ExitThread();
            return;
        }

        if (cycle.Advance(utc, elapsed))
        {
            if (cycle.OnBreak)
                BeginBreak(utc, elapsed);
            else
            {
                cycle.Sound = screen.SoundEnabled;
                CloseScreen();
                lastBreakEnd = elapsed;
                if (Checkpoint(utc, elapsed)) ApplyStagedUpdate();
            }
        }

        if (screen != null)
        {
            long remaining = cycle.Remaining(utc, elapsed);
            screen.ShowFrame((int)((remaining + 999) / 1000), PersistentCycle.BreakLength - remaining);
            if (elapsed >= nextCheckpoint || cycle.Sound != screen.SoundEnabled)
                Checkpoint(utc, elapsed);
        }
    }

    private void BeginBreak(long utc, long elapsed)
    {
        cycle.Season = SceneMotion.TakeSeason(Path.Combine(AutoUpdate.Profile, "season.txt"));
        Checkpoint(utc, elapsed);
        TargetWindows.RequestClose();
        ShowCurrentBreak(utc, elapsed);
    }

    private void ShowCurrentBreak(long utc, long elapsed)
    {
        long remaining = cycle.Remaining(utc, elapsed);
        ShowBreak(false, cycle.Season, cycle.Sound, (int)((remaining + 999) / 1000),
            PersistentCycle.BreakLength - remaining);
    }

    private void ShowBreak(bool demo, int season, bool sound, int seconds, long elapsed)
    {
        screen = new BreakScreen(sound, demo, season);
        screen.ShowFrame(seconds, elapsed);
        screen.Show();
        screen.Activate();
    }

    private void CloseScreen()
    {
        screen.Dismiss();
        screen.Dispose();
        screen = null;
    }

    private bool Checkpoint(long utc, long elapsed)
    {
        if (screen != null) cycle.Sound = screen.SoundEnabled;
        nextCheckpoint = elapsed + 5000;
        return store.Save(cycle.Snapshot(utc, elapsed));
    }

    private void SaveBeforeShutdown()
    {
        if (suspended != null) store.Save(suspended);
        else Checkpoint(UtcMilliseconds, clock.ElapsedMilliseconds);
    }

    private void Suspend()
    {
        if (suspended != null) return;
        if (screen != null) cycle.Sound = screen.SoundEnabled;
        suspended = cycle.Snapshot(UtcMilliseconds, clock.ElapsedMilliseconds);
        store.Save(suspended);
        clock.Stop();
    }

    private void Resume()
    {
        if (suspended == null) return;
        suspended = null;
        clock.Start();
    }

    private void ApplyStagedUpdate()
    {
        if (updateCheck == null || !updateCheck.IsCompleted) return;
        if (updateCheck.Status == TaskStatus.RanToCompletion && updateCheck.Result != null)
            AutoUpdate.Launch(updateCheck.Result);
        else if (updateCheck.IsFaulted)
            Debug.WriteLine(updateCheck.Exception);
        updateCheck = null;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            if (sessionWindow != null) sessionWindow.DestroyHandle();
            if (screen != null) screen.Dispose();
        }
        base.Dispose(disposing);
    }
}

internal sealed class SessionWindow : NativeWindow
{
    private readonly Action save;
    private readonly Action suspend;
    private readonly Action resume;

    public SessionWindow(Action save, Action suspend, Action resume)
    {
        this.save = save;
        this.suspend = suspend;
        this.resume = resume;
        CreateHandle(new CreateParams());
    }

    protected override void WndProc(ref Message m)
    {
        const int QueryEndSession = 0x0011;
        const int EndSession = 0x0016;
        const int PowerBroadcast = 0x0218;
        if (m.Msg == QueryEndSession || (m.Msg == EndSession && m.WParam != IntPtr.Zero))
            save();
        if (m.Msg == PowerBroadcast)
        {
            if (m.WParam.ToInt64() == 4) suspend();
            if (m.WParam.ToInt64() == 7 || m.WParam.ToInt64() == 18) resume();
        }
        base.WndProc(ref m);
        if (m.Msg == QueryEndSession) m.Result = new IntPtr(1);
    }
}

internal static class TargetWindows
{
    private const uint WmClose = 0x0010;
    private delegate bool WindowVisitor(IntPtr window, IntPtr parameter);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(WindowVisitor visitor, IntPtr parameter);
    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

    public static void RequestClose()
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "apps.txt");
        try
        {
            foreach (string line in File.ReadAllLines(path))
            {
                string name = line.Trim();
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    name = name.Substring(0, name.Length - 4);
                if (name.Length > 0 && !name.StartsWith("#")) targets.Add(name);
            }
        }
        catch (IOException) { return; }
        catch (UnauthorizedAccessException) { return; }

        if (targets.Count == 0) return;
        using (var current = Process.GetCurrentProcess())
        {
            uint ownId = (uint)current.Id;
            int sessionId = current.SessionId;
            EnumWindows(delegate(IntPtr window, IntPtr parameter)
            {
                if (!IsWindowVisible(window)) return true;
                uint processId;
                GetWindowThreadProcessId(window, out processId);
                if (processId == 0 || processId == ownId) return true;
                try
                {
                    using (var process = Process.GetProcessById((int)processId))
                    {
                        if (process.SessionId == sessionId && targets.Contains(process.ProcessName))
                            PostMessage(window, WmClose, IntPtr.Zero, IntPtr.Zero);
                    }
                }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
                catch (System.ComponentModel.Win32Exception) { }
                return true;
            }, IntPtr.Zero);
        }
    }
}
