@echo off
echo ========================================
echo Running EXE with PsExec as SYSTEM
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

REM Check if the executable exists
if not exist "src\KerberosConstrainedDelegation\bin\Debug\net9.0\KerberosConstrainedDelegation.exe" (
    echo ERROR: Executable not found. Building project first...
    echo.
    dotnet build src\KerberosConstrainedDelegation\KerberosConstrainedDelegation.csproj
    echo.
)

REM Run the executable as SYSTEM
echo Running: src\KerberosConstrainedDelegation\bin\Debug\net9.0\KerberosConstrainedDelegation.exe
echo.
C:\Tools\PSTools\psexec.exe -s -i src\KerberosConstrainedDelegation\bin\Debug\net9.0\KerberosConstrainedDelegation.exe

echo.
echo ========================================
echo Execution completed
echo Exit code: %ERRORLEVEL%
echo ========================================
pause
