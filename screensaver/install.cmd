@echo off
rem Installs the Shan Shui screensaver for the current user (no admin needed) and selects it.
setlocal
set "SRC=%~dp0ShanShui.scr"
set "DEST=%LOCALAPPDATA%\Programs\ShanShui"

if not exist "%SRC%" (
  echo ShanShui.scr was not found next to this script. Extract the whole zip first.
  pause & exit /b 1
)

rem The screensaver renders with the Edge WebView2 Runtime (preinstalled on Windows 11 and current Windows 10).
set "WV=0"
for %%K in ("HKLM\SOFTWARE\WOW6432Node" "HKLM\SOFTWARE" "HKCU\Software") do (
  reg query "%%~K\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}" /v pv >nul 2>&1 && set "WV=1"
)
if "%WV%"=="0" (
  echo WARNING: the Microsoft Edge WebView2 Runtime was not found. The screensaver needs it:
  echo   https://go.microsoft.com/fwlink/p/?LinkId=2124703
  echo.
)

if not exist "%DEST%" mkdir "%DEST%"
copy /y "%SRC%" "%DEST%\ShanShui.scr" >nul || (echo Could not copy to "%DEST%". & pause & exit /b 1)

rem Same registry values Windows writes for right-click ^> Install.
for %%I in ("%DEST%\ShanShui.scr") do set "SCR=%%~sI"
reg add "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE /t REG_SZ /d "%SCR%" /f >nul
reg add "HKCU\Control Panel\Desktop" /v ScreenSaveActive /t REG_SZ /d 1 /f >nul

echo Installed to %DEST%
echo Opening Screen Saver Settings: pick a wait time, use Settings... to choose Light or Dark, then click OK.
start "" control desk.cpl,,@screensaver
