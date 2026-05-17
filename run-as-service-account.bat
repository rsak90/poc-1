@echo off
REM ============================================================
REM  Run KerberosConstrainedDelegation under the service account
REM  so that SeTcbPrivilege and SeImpersonatePrivilege are active.
REM
REM  Requirements:
REM    - PsExec.exe must be in PATH or same folder as this script
REM    - Run this .bat as Administrator
REM    - Update EXE_PATH, DOMAIN, USERNAME, PASSWORD below
REM ============================================================

set EXE_PATH=D:\Services\KerberosApp\KerberosConstrainedDelegation.exe
set SVC_DOMAIN=CONTOSO
set SVC_USER=svc_delegation
set SVC_PASS=YourServiceAccountPassword

echo.
echo Running as %SVC_DOMAIN%\%SVC_USER% via PsExec...
echo EXE: %EXE_PATH%
echo.

psexec -accepteula -h ^
    -u %SVC_DOMAIN%\%SVC_USER% ^
    -p %SVC_PASS% ^
    "%EXE_PATH%"

echo.
echo Exit code: %ERRORLEVEL%
pause
