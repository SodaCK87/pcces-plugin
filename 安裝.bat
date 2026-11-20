@echo off
setlocal
rem Install the PCCES plugin into Claude Code. No admin rights needed.
rem PCX_PLUGIN_SOURCE is only for testing with a local folder; normally it is not set.
rem The HTTPS URL matters: "owner/repo" makes Claude Code clone over SSH, which fails
rem on a PC that has never connected to github.com over SSH.
set "MARKET=pcces-plugin"
set "SOURCE=https://github.com/SodaCK87/pcces-plugin.git"
if defined PCX_PLUGIN_SOURCE set "SOURCE=%PCX_PLUGIN_SOURCE%"
call :find_claude
if not defined CLAUDE goto no_claude
call :find_git
if not defined GITOK goto no_git

echo [1/2] Adding marketplace %SOURCE% ...
"%CLAUDE%" plugin marketplace add "%SOURCE%"
rem If it was added before this step fails; that is fine, go on.
echo.
echo [2/2] Installing pcces@%MARKET% ...
"%CLAUDE%" plugin install pcces@%MARKET%
if errorlevel 1 goto failed
echo.
echo Done. Restart Claude Code, then the PCCES tools are ready.
pause
exit /b 0

:failed
echo.
echo Install did not finish. Send a screenshot of this window to the person who gave you this tool.
pause
exit /b 1

:no_claude
echo Claude Code (claude.exe) was not found in PATH or in %USERPROFILE%\.local\bin.
echo Open Claude Code and type these two lines, one at a time:
echo.
echo   /plugin marketplace add https://github.com/SodaCK87/pcces-plugin.git
echo   /plugin install pcces@pcces-plugin
echo.
echo Then restart Claude Code.
pause
exit /b 2

:no_git
echo Git for Windows was not found. Claude Code needs it to download this plugin.
echo.
echo   1. Download it from https://git-scm.com/download/win
echo   2. Install it with the default options.
echo   3. Double-click this file again.
echo.
pause
exit /b 3

:find_claude
set "CLAUDE="
for /f "delims=" %%P in ('where claude.exe 2^>nul') do if not defined CLAUDE set "CLAUDE=%%P"
if not defined CLAUDE if exist "%USERPROFILE%\.local\bin\claude.exe" set "CLAUDE=%USERPROFILE%\.local\bin\claude.exe"
exit /b 0

rem Git installed but not on PATH (installer option, or PATH not refreshed yet):
rem put its cmd folder in front of PATH so claude.exe started from here finds it.
rem PCX_PROGRAMFILES is only for testing: Windows resets ProgramFiles for every new process.
:find_git
set "GITOK="
if not defined PCX_PROGRAMFILES set "PCX_PROGRAMFILES=%ProgramFiles%"
where git.exe >nul 2>nul
if not errorlevel 1 set "GITOK=1"
if defined GITOK exit /b 0
if exist "%PCX_PROGRAMFILES%\Git\cmd\git.exe" set "PATH=%PCX_PROGRAMFILES%\Git\cmd;%PATH%"
if exist "%PCX_PROGRAMFILES%\Git\cmd\git.exe" set "GITOK=1"
if defined GITOK exit /b 0
if exist "%LOCALAPPDATA%\Programs\Git\cmd\git.exe" set "PATH=%LOCALAPPDATA%\Programs\Git\cmd;%PATH%"
if exist "%LOCALAPPDATA%\Programs\Git\cmd\git.exe" set "GITOK=1"
exit /b 0
