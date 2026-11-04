$ErrorActionPreference = 'Stop'
$app = Join-Path $env:LOCALAPPDATA 'VisionBreak'
$exe = Join-Path $app 'VisionBreak.exe'
$state = Join-Path $app 'cycle.txt'
$setup = (Resolve-Path "dist\VisionBreak-Setup-$env:VISIONBREAK_VERSION.exe").Path
$future = '99.0.0'
$futureSetup = "dist\VisionBreak-Setup-$future.exe"
$versionSource = 'src\ReleaseCheck.cs'
$original = [IO.File]::ReadAllText($versionSource)
try {
    $installed = Start-Process $setup -ArgumentList '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART' -Wait -PassThru
    if ($installed.ExitCode -ne 0) { throw 'Initial setup failed' }
    [IO.File]::WriteAllText($versionSource, $original.Replace('CurrentVersion = "' + $env:VISIONBREAK_VERSION + '"', 'CurrentVersion = "' + $future + '"'))
    .\Build.cmd
    if ($LASTEXITCODE -ne 0) { throw 'Future application compilation failed' }
    $iscc = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
    & $iscc "/DAppVersion=$future" installer\VisionBreak.iss
    if ($LASTEXITCODE -ne 0) { throw 'Future installer compilation failed' }
    $staged = Join-Path $app "updates\$future"
    New-Item -ItemType Directory -Force $staged | Out-Null
    Copy-Item $exe (Join-Path $staged 'previous.exe')
    Copy-Item $futureSetup (Join-Path $staged 'setup.exe')
    New-Item -ItemType File -Force (Join-Path $staged 'attempted') | Out-Null
    $hash = (Get-FileHash (Join-Path $staged 'setup.exe')).Hash
    [IO.File]::WriteAllText($state, '1|break|9000|2|False')
    $normal = Start-Process $exe -PassThru
    $rejected = Start-Process $exe -ArgumentList '--stop-for-update' -Wait -PassThru
    if ($rejected.ExitCode -eq 0 -or $normal.HasExited) { throw 'Update stop bypassed an active break' }
    $watch = [Diagnostics.Stopwatch]::StartNew()
    do {
        if ($watch.Elapsed.TotalSeconds -gt 12) { throw 'Break did not finish' }
        Start-Sleep -Milliseconds 100
        try { $before = [IO.File]::ReadAllText($state) } catch [IO.IOException] { $before = '' }
    } until ($before.StartsWith('1|use|'))
    $deadline = $before.Split('|')[2]
    $badHash = '0' * 64
    $invalid = Start-Process (Join-Path $staged 'previous.exe') -ArgumentList "--apply-update $future $badHash" -PassThru
    if (!$invalid.WaitForExit(10000) -or $normal.HasExited -or
        [Reflection.AssemblyName]::GetAssemblyName($exe).Version.ToString(3) -ne $env:VISIONBREAK_VERSION) {
        throw 'Unverified package changed the installation'
    }
    $helper = Start-Process (Join-Path $staged 'previous.exe') -ArgumentList "--apply-update $future $hash" -PassThru
    if (!$helper.WaitForExit(35000) -or !$normal.WaitForExit(5000)) { throw 'Automatic update did not finish' }
    $actual = [Reflection.AssemblyName]::GetAssemblyName($exe).Version.ToString(3)
    if ($actual -ne $future) { throw "Unexpected installed version $actual" }
    $watch.Restart()
    do {
        if ($watch.Elapsed.TotalSeconds -gt 10) { throw 'Updated process did not restart' }
        Start-Sleep -Milliseconds 150
        $running = Get-Process VisionBreak -ErrorAction SilentlyContinue | Where-Object Path -eq $exe
    } until ($running)
    if (([IO.File]::ReadAllText($state)).Split('|')[2] -ne $deadline) { throw 'Update reset the use deadline' }
    'Automatic update respected the break boundary, restarted and retained the deadline.'
}
finally {
    [IO.File]::WriteAllText($versionSource, $original)
    Remove-Item $futureSetup -Force -ErrorAction SilentlyContinue
    if (Test-Path $exe) { Start-Process $exe -ArgumentList '--stop' -Wait }
}
