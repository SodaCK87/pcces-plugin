@echo off
rem Launch PCCES at medium integrity so Claude computer-use can click it.
chcp 65001 >nul
set "PS1="
for %%F in ("%~dp0*PCCES*.ps1") do set "PS1=%%~fF"
if not defined PS1 (
  echo Launcher .ps1 not found next to this .bat
  pause
  exit /b 2
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%PS1%"
pause
