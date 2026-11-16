$ErrorActionPreference = 'Stop'
$setup = (Resolve-Path "dist\VisionBreak-Setup-$env:VISIONBREAK_VERSION.exe").Path
$result = Start-Process $setup -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru
if ($result.ExitCode -ne 0) { throw 'Setup failed' }
$app = Join-Path $env:LOCALAPPDATA 'VisionBreak'
$exe = Join-Path $app 'VisionBreak.exe'
$state = Join-Path $app 'cycle.txt'
[IO.File]::WriteAllText($state, '1|break|120000|1|False')
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class EmergencyInput {
    private delegate bool Visitor(IntPtr window, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Visitor visitor, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);
    public static bool HasVisibleWindow(uint process) {
        bool found = false;
        EnumWindows(delegate(IntPtr window, IntPtr parameter) {
            uint owner;
            GetWindowThreadProcessId(window, out owner);
            if (owner == process && IsWindowVisible(window)) found = true;
            return true;
        }, IntPtr.Zero);
        return found;
    }
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extra);
}
'@
$process = Start-Process $exe -PassThru
try {
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        Start-Sleep -Milliseconds 100
        if ($process.HasExited -or $watch.Elapsed.TotalSeconds -gt 10) { throw ("Break screen did not open. Local={0}, Zone={1}, Exited={2}, State={3}, Title={4}" -f [DateTime]::Now, [TimeZoneInfo]::Local.Id, $process.HasExited, [IO.File]::ReadAllText($state), $process.MainWindowTitle) }
        $screen = [EmergencyInput]::HasVisibleWindow($process.Id)
    } until ($screen)
    Start-Sleep -Milliseconds 250
    $keys = @(0x11, 0x12, 0x10, 0x7B)
    try {
        foreach ($key in $keys) { [EmergencyInput]::keybd_event($key, 0, 0, [UIntPtr]::Zero) }
        Start-Sleep -Milliseconds 250
    }
    finally {
        foreach ($key in ($keys | Sort-Object -Descending)) { [EmergencyInput]::keybd_event($key, 0, 2, [UIntPtr]::Zero) }
    }
    if (!$process.WaitForExit(5000) -or $process.ExitCode -ne 0) { throw 'Emergency shortcut did not exit normally' }
    if ([EmergencyInput]::HasVisibleWindow($process.Id)) { throw 'Break screen remained visible' }
    $saved = [IO.File]::ReadAllText($state).Split('|')
    if ($saved[1] -ne 'break' -or [long]$saved[2] -lt 100000) { throw 'Emergency exit lost the remaining break' }
    'Global emergency shortcut closed an active break and saved its remaining time.'
}
finally {
    if (!$process.HasExited) { Start-Process $exe -ArgumentList '--stop' -Wait }
}
