@echo off
setlocal
rem Update the PCCES plugin. Both steps are needed: the plugin only moves to a new
rem version after the marketplace itself has been updated.
set "MARKET=pcces-plugin"
call :find_claude
if not defined CLAUDE goto no_claude
call :find_git
if not defined GITOK goto no_git

echo [1/2] Updating marketplace %MARKET% ...
"%CLAUDE%" plugin marketplace update %MARKET%
if errorlevel 1 goto failed
echo.
echo [2/2] Updating pcces@%MARKET% ...
"%CLAUDE%" plugin update pcces@%MARKET%
if errorlevel 1 goto failed
echo.
echo Done. Restart Claude Code to use the new version.
pause
exit /b 0

:failed
echo.
echo Update did not finish. Send a screenshot of this window to the person who gave you this tool.
pause
exit /b 1

:no_claude
echo Claude Code (claude.exe) was not found in PATH or in %USERPROFILE%\.local\bin.
echo Open Claude Code and type these three lines, one at a time:
echo.
echo   /plugin marketplace update pcces-plugin
echo   /plugin uninstall pcces@pcces-plugin
echo   /plugin install pcces@pcces-plugin
echo.
echo Then restart Claude Code.
pause
exit /b 2

:no_git
echo Git for Windows was not found. Claude Code needs it to download updates.
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

rem Git installed but not on PATH: put its cmd folder in front of PATH for claude.exe.
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
