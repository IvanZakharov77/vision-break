@echo off
setlocal
cd /d "%~dp0"
set "framework=%SystemRoot%\Microsoft.NET\Framework\v4.0.30319"
if not exist "%framework%\csc.exe" (
    echo .NET Framework 4.x compiler was not found.
    exit /b 1
)
set "refs=/reference:%framework%\System.Net.Http.dll /reference:%framework%\System.Web.Extensions.dll"
if "%~1"=="--test" goto tests
"%framework%\csc.exe" /nologo /target:winexe /optimize+ /codepage:65001 %refs% /out:VisionBreak.exe /reference:"%framework%\System.Drawing.dll" /reference:"%framework%\System.Windows.Forms.dll" src\Program.cs src\BreakCycle.cs src\BreakScreen.cs src\AutoUpdate.cs src\Landscape.cs src\SceneMotion.cs src\ReleaseCheck.cs src\PersistentCycle.cs
exit /b %errorlevel%

:tests
set "testexe=%TEMP%\VisionBreakTests-%RANDOM%-%RANDOM%.exe"
"%framework%\csc.exe" /nologo /target:exe /codepage:65001 %refs% /out:"%testexe%" tests\CycleTests.cs tests\ReleaseTests.cs tests\PersistenceTests.cs tests\UpdateTests.cs tests\SceneTests.cs src\SceneMotion.cs src\BreakCycle.cs src\ReleaseCheck.cs src\PersistentCycle.cs
if errorlevel 1 exit /b 1
"%testexe%"
set "result=%errorlevel%"
del /q "%testexe%"
exit /b %result%
