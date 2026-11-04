@echo off
setlocal
cd /d "%~dp0"
call "%~dp0Build.cmd"
if errorlevel 1 goto failed
set "VISIONBREAK_DIR=%LOCALAPPDATA%\VisionBreak"
if not exist "%VISIONBREAK_DIR%\" mkdir "%VISIONBREAK_DIR%"
if errorlevel 1 goto failed
if exist "%VISIONBREAK_DIR%\VisionBreak.exe" (
    start "" /wait "%VISIONBREAK_DIR%\VisionBreak.exe" --stop
    if errorlevel 1 goto failed
)
copy /y "VisionBreak.exe" "%VISIONBREAK_DIR%\VisionBreak.exe" >nul || goto failed
copy /y "Remove.cmd" "%VISIONBREAK_DIR%\Remove.cmd" >nul || goto failed
if not exist "%VISIONBREAK_DIR%\apps.txt" (
    copy /y "apps.txt" "%VISIONBREAK_DIR%\apps.txt" >nul || goto failed
)
powershell.exe -NoProfile -NonInteractive -Command "$ErrorActionPreference='Stop'; $startup=[Environment]::GetFolderPath('Startup'); $shell=New-Object -ComObject WScript.Shell; $link=$shell.CreateShortcut((Join-Path $startup 'VisionBreak.lnk')); $link.TargetPath=Join-Path $env:VISIONBREAK_DIR 'VisionBreak.exe'; $link.WorkingDirectory=$env:VISIONBREAK_DIR; $link.Save()"
if errorlevel 1 goto failed
start "" "%VISIONBREAK_DIR%\VisionBreak.exe"
if errorlevel 1 goto failed
echo Installed for the current Windows account.
pause
exit /b 0

:failed
echo Installation failed. See the error above.
pause
exit /b 1
