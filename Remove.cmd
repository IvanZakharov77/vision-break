@echo off
setlocal
cd /d "%TEMP%"
set "VISIONBREAK_DIR=%LOCALAPPDATA%\VisionBreak"
if exist "%VISIONBREAK_DIR%\VisionBreak.exe" (
    start "" /wait "%VISIONBREAK_DIR%\VisionBreak.exe" --stop
    if errorlevel 1 goto failed
)
powershell.exe -NoProfile -NonInteractive -Command "$ErrorActionPreference='Stop'; $link=Join-Path ([Environment]::GetFolderPath('Startup')) 'VisionBreak.lnk'; if(Test-Path -LiteralPath $link){Remove-Item -LiteralPath $link}"
if errorlevel 1 goto failed
if exist "%VISIONBREAK_DIR%\VisionBreak.exe" (
    del /q "%VISIONBREAK_DIR%\VisionBreak.exe"
    if exist "%VISIONBREAK_DIR%\VisionBreak.exe" goto failed
)
echo Removed. Your apps.txt settings were kept.
pause
exit /b 0

:failed
echo Removal failed. See the error above.
pause
exit /b 1
