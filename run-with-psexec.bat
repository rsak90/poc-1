@echo off
echo ========================================
echo Running with PsExec as SYSTEM
echo ========================================
echo.

REM Change to the project directory
cd /d "%~dp0"

REM Check if PsExec exists
if not exist "C:\Tools\PSTools\psexec.exe" (
    echo ERROR: PsExec not found at C:\Tools\PSTools\psexec.exe
    echo.
    echo Please download PsExec from:
    echo https://learn.microsoft.com/en-us/sysinternals/downloads/psexec
    echo.
    echo Extract it to C:\Tools\PSTools\
    echo.
    pause
    exit /b 1
)

REM Run the application as SYSTEM
echo Running: dotnet run --project src\KerberosConstrainedDelegation\KerberosConstrainedDelegation.csproj
echo.
C:\Tools\PSTools\psexec.exe -s -i dotnet run --project src\KerberosConstrainedDelegation\KerberosConstrainedDelegation.csproj

echo.
echo ========================================
echo Execution completed
echo Exit code: %ERRORLEVEL%
echo ========================================
pause
