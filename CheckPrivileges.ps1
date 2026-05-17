# Check if SeTcbPrivilege is available
Write-Host "=== Checking SeTcbPrivilege Status ===" -ForegroundColor Cyan
Write-Host ""

# Check current user
Write-Host "Current User: $env:USERNAME" -ForegroundColor Yellow
Write-Host "Domain: $env:USERDOMAIN" -ForegroundColor Yellow
Write-Host ""

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if ($isAdmin) {
    Write-Host "[OK] Running as Administrator" -ForegroundColor Green
} else {
    Write-Host "[ERROR] NOT running as Administrator" -ForegroundColor Red
    Write-Host "  You must run this script as Administrator!" -ForegroundColor Red
    exit
}
Write-Host ""

# Check privileges
Write-Host "Current Process Privileges:" -ForegroundColor Yellow
$privileges = whoami /priv | Select-String "SeTcb"
if ($privileges) {
    Write-Host $privileges -ForegroundColor Green
    Write-Host ""
    Write-Host "[OK] SeTcbPrivilege is available!" -ForegroundColor Green
    Write-Host "  The application should be able to enable it." -ForegroundColor Green
} else {
    Write-Host "[ERROR] SeTcbPrivilege is NOT available" -ForegroundColor Red
    Write-Host ""
    Write-Host "You need to grant SeTcbPrivilege to your account:" -ForegroundColor Yellow
    Write-Host "1. Run: secpol.msc" -ForegroundColor White
    Write-Host "2. Navigate to: Local Policies -> User Rights Assignment" -ForegroundColor White
    Write-Host "3. Find: 'Act as part of the operating system'" -ForegroundColor White
    Write-Host "4. Add your account: $env:USERDOMAIN\$env:USERNAME" -ForegroundColor White
    Write-Host "5. Log out and log back in" -ForegroundColor White
}
Write-Host ""

# Check who has the privilege
Write-Host "Accounts with SeTcbPrivilege:" -ForegroundColor Yellow
try {
    $secpol = secedit /export /cfg "$env:TEMP\secpol.cfg" /quiet
    $content = Get-Content "$env:TEMP\secpol.cfg"
    $tcbLine = $content | Select-String "SeTcbPrivilege"
    if ($tcbLine) {
        Write-Host $tcbLine -ForegroundColor Cyan
    } else {
        Write-Host "  No accounts found" -ForegroundColor Gray
    }
    Remove-Item "$env:TEMP\secpol.cfg" -ErrorAction SilentlyContinue
} catch {
    Write-Host "  Could not retrieve policy information" -ForegroundColor Gray
}
Write-Host ""

Write-Host "=== Next Steps ===" -ForegroundColor Cyan
if ($privileges) {
    Write-Host "[OK] You are ready! Run your application as Administrator." -ForegroundColor Green
} else {
    Write-Host "1. Grant SeTcbPrivilege using secpol.msc (see instructions above)" -ForegroundColor Yellow
    Write-Host "2. Log out and log back in (or restart)" -ForegroundColor Yellow
    Write-Host "3. Run this script again to verify" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Alternative: Use PsExec to run as SYSTEM" -ForegroundColor Cyan
    Write-Host "  psexec.exe -s -i dotnet run" -ForegroundColor White
}
