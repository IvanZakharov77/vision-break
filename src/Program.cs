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

    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--stop")
        {
            Environment.ExitCode = StopRunningInstance();
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
                using (var session = new BreakSession(demo, stop))
                    Application.Run(session);
            }
            finally
            {
                instance.ReleaseMutex();
            }
        }
    }

    private static int StopRunningInstance()
    {
        try
        {
            using (var instance = Mutex.OpenExisting(InstanceName))
            {
                try
                {
                    using (var signal = EventWaitHandle.OpenExisting(StopName))
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
    private readonly BreakCycle cycle;
    private readonly System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer { Interval = 100 };
    private readonly bool demo;
    private readonly EventWaitHandle stop;
    private Task<Version> updateCheck;
    private NotifyIcon updateNotice;
    private BreakScreen screen;
    private bool sound = true;

    public BreakSession(bool demo, EventWaitHandle stop)
    {
        this.demo = demo;
        this.stop = stop;
        cycle = new BreakCycle(clock.ElapsedMilliseconds, demo);
        if (demo)
            ShowBreak();
        timer.Tick += OnTick;
        timer.Start();
        if (!demo)
            updateCheck = Task.Run(() => ReleaseCheck.CheckAsync());
    }

    private void OnTick(object sender, EventArgs e)
    {
        if (stop != null && stop.WaitOne(0))
        {
            ExitThread();
            return;
        }

        if (updateCheck != null && updateCheck.IsCompleted && screen == null)
            ShowUpdateIfAvailable();

        long now = clock.ElapsedMilliseconds;
        if (cycle.Advance(now))
        {
            if (cycle.OnBreak)
            {
                TargetWindows.RequestClose();
                ShowBreak();
            }
            else
            {
                sound = screen.SoundEnabled;
                screen.Dismiss();
                screen.Dispose();
                screen = null;
                if (demo) ExitThread();
            }
        }
        if (screen != null)
            screen.ShowSeconds(cycle.RemainingSeconds(clock.ElapsedMilliseconds));
    }

    private void ShowUpdateIfAvailable()
    {
        Task<Version> result = updateCheck;
        updateCheck = null;
        if (result.IsFaulted)
        {
            Debug.WriteLine(result.Exception);
            return;
        }
        if (result.IsCanceled || result.Result == null) return;

        var menu = new ContextMenu();
        menu.MenuItems.Add("Открыть страницу обновления", delegate { OpenReleasePage(); });
        updateNotice = new NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Text = "VisionBreak " + result.Result + " доступна",
            ContextMenu = menu,
            Visible = true
        };
        updateNotice.BalloonTipClicked += delegate { OpenReleasePage(); };
        updateNotice.DoubleClick += delegate { OpenReleasePage(); };
        updateNotice.ShowBalloonTip(6000, "Обновление VisionBreak",
            "Доступна версия " + result.Result + ". Нажмите, чтобы открыть страницу загрузки.",
            ToolTipIcon.Info);
    }

    private static void OpenReleasePage()
    {
        try
        {
            Process.Start(new ProcessStartInfo(ReleaseCheck.ReleasePage) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception)
        {
            MessageBox.Show(ReleaseCheck.ReleasePage, "Страница обновления");
        }
    }

    private void ShowBreak()
    {
        screen = new BreakScreen(sound, demo);
        screen.ShowSeconds(cycle.RemainingSeconds(clock.ElapsedMilliseconds));
        screen.Show();
        screen.Activate();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            timer.Dispose();
            if (updateNotice != null)
            {
                updateNotice.Visible = false;
                updateNotice.ContextMenu.Dispose();
                updateNotice.Dispose();
            }
            if (screen != null) screen.Dispose();
        }
        base.Dispose(disposing);
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
