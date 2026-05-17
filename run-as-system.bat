@echo off
REM ============================================================
REM  Run KerberosConstrainedDelegation as SYSTEM.
REM  SYSTEM has SeTcbPrivilege by default — useful for quick testing
REM  before setting up the service account properly.
REM
REM  Requirements:
REM    - PsExec.exe must be in PATH or same folder as this script
REM    - Run this .bat as Administrator
REM ============================================================

set EXE_PATH=D:\Services\KerberosApp\KerberosConstrainedDelegation.exe

echo.
echo Running as SYSTEM via PsExec...
echo EXE: %EXE_PATH%
echo.

psexec -accepteula -s -i "%EXE_PATH%"

echo.
echo Exit code: %ERRORLEVEL%
pause
