@echo off
setlocal
rem Install the PCCES plugin into Claude Code. No admin rights needed.
rem PCX_PLUGIN_SOURCE is only for testing with a local folder; normally it is not set.
set "MARKET=pcces-plugin"
set "SOURCE=SodaCK87/pcces-plugin"
if defined PCX_PLUGIN_SOURCE set "SOURCE=%PCX_PLUGIN_SOURCE%"
call :find_claude
if not defined CLAUDE goto no_claude

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
echo   /plugin marketplace add SodaCK87/pcces-plugin
echo   /plugin install pcces@pcces-plugin
echo.
echo Then restart Claude Code.
pause
exit /b 2

:find_claude
set "CLAUDE="
for /f "delims=" %%P in ('where claude.exe 2^>nul') do if not defined CLAUDE set "CLAUDE=%%P"
if not defined CLAUDE if exist "%USERPROFILE%\.local\bin\claude.exe" set "CLAUDE=%USERPROFILE%\.local\bin\claude.exe"
exit /b 0
