@echo off
rem Removes the Shan Shui screensaver, its settings and its browser cache for the current user.
setlocal
set "DEST=%LOCALAPPDATA%\Programs\ShanShui"

rem Deselect it only if it is the active screensaver.
for /f "tokens=2,*" %%A in ('reg query "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE 2^>nul ^| find /i "SCRNSAVE.EXE"') do set "CUR=%%B"
echo "%CUR%" | find /i "shanshui" >nul && reg delete "HKCU\Control Panel\Desktop" /v SCRNSAVE.EXE /f >nul

reg delete "HKCU\Software\ShanShuiSaver" /f >nul 2>&1
if exist "%DEST%" rmdir /s /q "%DEST%"
rem Native DLLs the self-contained .scr extracts on first run.
if exist "%TEMP%\.net\ShanShui.scr" rmdir /s /q "%TEMP%\.net\ShanShui.scr"
rem The browser cache can stay locked for a few seconds after the screensaver closes.
for /l %%N in (1,1,10) do if exist "%LOCALAPPDATA%\ShanShuiSaver" (
  rmdir /s /q "%LOCALAPPDATA%\ShanShuiSaver" 2>nul
  if exist "%LOCALAPPDATA%\ShanShuiSaver" timeout /t 1 /nobreak >nul
)
if exist "%LOCALAPPDATA%\ShanShuiSaver" echo Could not remove "%LOCALAPPDATA%\ShanShuiSaver" - close the screensaver and delete it by hand.
echo Shan Shui screensaver removed.
pause
