# Troubleshooting Guide

This guide provides solutions for common errors and issues when using the Kerberos Constrained Delegation application.

## Table of Contents

1. [Common Error Scenarios](#common-error-scenarios)
2. [Diagnostic Commands](#diagnostic-commands)
3. [Delegation Configuration Verification](#delegation-configuration-verification)
4. [SPN Registration Verification](#spn-registration-verification)
5. [Network and Connectivity Issues](#network-and-connectivity-issues)
6. [Event Log Analysis](#event-log-analysis)
7. [Advanced Troubleshooting](#advanced-troubleshooting)

## Common Error Scenarios

### Error: "Service authentication failed" (Win32 Error: 0x8007052E / 1326)

**Full Error Message:**
```
[ERROR] Kerberos error: Service authentication failed
[ERROR] Error type: ServiceAuthenticationFailed
[ERROR] Win32 error code: 0x8007052E (1326 - ERROR_LOGON_FAILURE)
```

**Cause:**
- Service account credentials are incorrect
- Service account is disabled or locked
- Service account password has expired
- Domain controller is unreachable

**Solutions:**

1. **Verify Service Account Credentials:**
```powershell
# Test credentials manually
$cred = Get-Credential -UserName "CONTOSO\svc_delegation"
Test-Connection -ComputerName DC01 -Credential $cred
```

2. **Check Account Status:**
```powershell
# Check if account is enabled
Get-ADUser -Identity svc_delegation | Select-Object Enabled, LockedOut, PasswordExpired

# Unlock account if locked
Unlock-ADAccount -Identity svc_delegation

# Enable account if disabled
Enable-ADAccount -Identity svc_delegation
```

3. **Reset Password:**
```powershell
# Reset service account password
Set-ADAccountPassword -Identity svc_delegation -Reset -NewPassword (ConvertTo-SecureString "NewP@ssw0rd123!" -AsPlainText -Force)

# Update appsettings.json with new password
```

4. **Verify Domain Controller Connectivity:**
```powershell
# Test connection to domain controller
Test-Connection -ComputerName DC01 -Count 4

# Verify DNS resolution
nslookup DC01.contoso.com

# Check Kerberos port
Test-NetConnection -ComputerName DC01 -Port 88
```

---

### Error: "Service not trusted for delegation" (Win32 Error: 0x80070032 / 50)

**Full Error Message:**
```
[ERROR] Kerberos error: Service account is not trusted for delegation to: cifs/fileserver.contoso.com
[ERROR] Error type: DelegationNotConfigured
[ERROR] Win32 error code: 0x80070032 (50 - ERROR_NOT_SUPPORTED)
```

**Cause:**
- Service account is not configured for constrained delegation
- Target SPN is not in the allowed delegation list
- Protocol transition is not enabled

**Solutions:**

1. **Verify Delegation Configuration:**
```powershell
# Check delegation settings
Get-ADUser -Identity svc_delegation -Properties msDS-AllowedToDelegateTo, TrustedToAuthForDelegation | 
    Select-Object Name, TrustedToAuthForDelegation, @{
        Name='AllowedToDelegateTo'
        Expression={$_.'msDS-AllowedToDelegateTo'}
    }
```

2. **Configure Constrained Delegation:**
```powershell
# Add target SPN to delegation list
Set-ADUser -Identity svc_delegation -Add @{
    'msDS-AllowedToDelegateTo' = @('cifs/fileserver.contoso.com', 'cifs/fileserver')
}

# Enable protocol transition (required for S4U2Self)
Set-ADAccountControl -Identity svc_delegation -TrustedToAuthForDelegation $true
```

3. **Verify via GUI:**
   - Open Active Directory Users and Computers
   - Find service account `svc_delegation`
   - Go to **Delegation** tab
   - Ensure "Trust this user for delegation to specified services only" is selected
   - Ensure "Use any authentication protocol" is selected
   - Verify target SPN is in the list

4. **Wait for Replication:**
```powershell
# Force Active Directory replication
repadmin /syncall /AdeP

# Wait 5-10 minutes for replication to complete
```

---

### Error: "User not found" (Win32 Error: 0x80070525 / 1317)

**Full Error Message:**
```
[ERROR] Kerberos error: S4U2Self failed for user: testuser@contoso.com
[ERROR] Error type: UserNotFound
[ERROR] Win32 error code: 0x80070525 (1317 - ERROR_NO_SUCH_USER)
```

**Cause:**
- Target username does not exist in Active Directory
- Username format is incorrect
- User account is disabled

**Solutions:**

1. **Verify User Exists:**
```powershell
# Check if user exists
Get-ADUser -Identity testuser

# Check with UPN
Get-ADUser -Filter {UserPrincipalName -eq "testuser@contoso.com"}
```

2. **Check Username Format:**
```powershell
# Valid formats:
# - UPN: testuser@contoso.com
# - DOMAIN\username: CONTOSO\testuser

# Test both formats in configuration
```

3. **Verify User is Enabled:**
```powershell
# Check user status
Get-ADUser -Identity testuser | Select-Object Enabled, LockedOut

# Enable user if disabled
Enable-ADAccount -Identity testuser
```

4. **Check "Account is sensitive and cannot be delegated" Flag:**
```powershell
# Check if user is marked as sensitive
Get-ADUser -Identity testuser -Properties AccountNotDelegated | Select-Object AccountNotDelegated

# If true, delegation will fail. Remove flag:
Set-ADUser -Identity testuser -AccountNotDelegated $false
```

---

### Error: "Access denied" when writing to file share (Exit Code: 5)

**Full Error Message:**
```
[ERROR] Process exit code: 5
[ERROR] Standard Error:
Access denied: \\fileserver.contoso.com\TestShare\test.txt
User CONTOSO\testuser does not have write permissions
```

**Cause:**
- Target user does not have write permissions on the file share
- NTFS permissions are too restrictive
- SMB signing requirements not met

**Solutions:**

1. **Verify Share Permissions:**
```powershell
# Check SMB share permissions
Get-SmbShareAccess -Name TestShare

# Grant permissions to user
Grant-SmbShareAccess -Name TestShare -AccountName "CONTOSO\testuser" -AccessRight Change -Force
```

2. **Verify NTFS Permissions:**
```powershell
# Check NTFS permissions
Get-Acl "C:\Shares\TestShare" | Format-List

# Grant NTFS permissions
$acl = Get-Acl "C:\Shares\TestShare"
$permission = "CONTOSO\testuser", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)
Set-Acl "C:\Shares\TestShare" $acl
```

3. **Test Access Manually:**
```powershell
# Test as target user
$cred = Get-Credential -UserName "CONTOSO\testuser"
New-Item -Path "\\fileserver.contoso.com\TestShare\test.txt" -ItemType File -Credential $cred -Force
```

4. **Check SMB Signing:**
```powershell
# Check SMB signing configuration
Get-SmbServerConfiguration | Select-Object RequireSecuritySignature, EnableSecuritySignature

# If required, ensure client supports it
Get-SmbClientConfiguration | Select-Object RequireSecuritySignature, EnableSecuritySignature
```

---

### Error: "S4U2Self failed" (Win32 Error: 0x8009030E / -2146893042)

**Full Error Message:**
```
[ERROR] Kerberos error: LsaLogonUser (S4U2Self) failed with status: 0xC000006D
[ERROR] Error type: S4U2SelfFailed
[ERROR] Win32 error code: 0x8009030E (SEC_E_NO_CREDENTIALS)
```

**Cause:**
- Service account does not have "Act as part of the operating system" privilege
- LSA authentication package not available
- Service account credentials expired during operation

**Solutions:**

1. **Grant Required Privileges:**
```powershell
# Via Group Policy:
# Computer Configuration > Windows Settings > Security Settings > Local Policies > User Rights Assignment
# Add svc_delegation to "Act as part of the operating system"

# Or use Local Security Policy (secpol.msc) on the local machine
```

2. **Verify LSA Package:**
```powershell
# Check if Negotiate package is available
# This is typically always available on Windows, but verify:
Get-Service -Name LanmanServer, LanmanWorkstation | Select-Object Name, Status
```

3. **Re-authenticate Service Account:**
```powershell
# Clear cached credentials
klist purge

# Re-run the application to force fresh authentication
```

---

### Error: "S4U2Proxy failed" (Win32 Error: 0x80090302 / -2146893054)

**Full Error Message:**
```
[ERROR] Kerberos error: LsaLogonUser (S4U2Proxy) failed with status: 0xC00000DC
[ERROR] Error type: S4U2ProxyFailed
[ERROR] Win32 error code: 0x80090302 (SEC_E_UNSUPPORTED_FUNCTION)
```

**Cause:**
- S4U2Self token is not forwardable
- Target SPN is not registered
- KDC does not support S4U2Proxy

**Solutions:**

1. **Verify SPN Registration:**
```powershell
# Check if target SPN is registered
setspn -Q cifs/fileserver.contoso.com

# If not found, register it:
setspn -A cifs/fileserver.contoso.com fileserver
```

2. **Check Domain Functional Level:**
```powershell
# S4U2Proxy requires Windows Server 2008 or higher
Get-ADDomain | Select-Object DomainMode

# Upgrade if necessary (requires planning and testing)
```

3. **Verify Token is Forwardable:**
```powershell
# This is checked internally by the application
# If this error occurs, check Event Logs for more details
Get-WinEvent -LogName Security -MaxEvents 100 | Where-Object {$_.Id -eq 4769}
```

---

### Error: "Process spawning failed" (Win32 Error: 0x80070005 / 5)

**Full Error Message:**
```
[ERROR] Kerberos error: CreateProcessAsUser failed with error: 0x80070005
[ERROR] Error type: ProcessSpawnFailed
[ERROR] Win32 error code: 0x80070005 (5 - ERROR_ACCESS_DENIED)
```

**Cause:**
- Insufficient permissions to spawn process with token
- Executable path is invalid or inaccessible
- Token is invalid or expired

**Solutions:**

1. **Verify Executable Path:**
```powershell
# Check if executable exists
Test-Path "C:\Apps\KerberosDelegation\FileShareWriter.exe"

# Check file permissions
Get-Acl "C:\Apps\KerberosDelegation\FileShareWriter.exe" | Format-List
```

2. **Run as Administrator:**
```powershell
# Run PowerShell as Administrator
# Then run the application
cd C:\Apps\KerberosDelegation
dotnet KerberosConstrainedDelegation.dll
```

3. **Check Token Validity:**
```powershell
# View current Kerberos tickets
klist

# If expired, purge and re-authenticate
klist purge
```

---

### Error: "Configuration validation failed"

**Full Error Message:**
```
[ERROR] Configuration validation failed: Service account username cannot be null or empty
```

**Cause:**
- Configuration file is missing or incomplete
- Required configuration values are not set

**Solutions:**

1. **Verify Configuration File Exists:**
```powershell
# Check if appsettings.json exists
Test-Path "C:\Apps\KerberosDelegation\appsettings.json"

# If missing, copy from template
Copy-Item "appsettings.template.json" "appsettings.json"
```

2. **Validate Configuration Format:**
```json
{
  "ServiceAccount": {
    "Username": "svc_delegation",  // Required
    "Domain": "CONTOSO",            // Required
    "Password": "P@ssw0rd"          // Required
  },
  "Delegation": {
    "TargetUsername": "testuser@contoso.com",              // Required
    "TargetServicePrincipalName": "cifs/fileserver.contoso.com",  // Required
    "ExternalExecutablePath": "C:\\Path\\To\\FileShareWriter.exe", // Required
    "FileSharePath": "\\\\fileserver\\share\\test.txt"     // Required
  }
}
```

3. **Check for JSON Syntax Errors:**
```powershell
# Validate JSON syntax
Get-Content "appsettings.json" | ConvertFrom-Json
```

---

## Diagnostic Commands

### Kerberos Ticket Management

```powershell
# View current Kerberos tickets
klist

# View detailed ticket information
klist tickets

# Purge all tickets (force re-authentication)
klist purge

# Request specific ticket
klist get cifs/fileserver.contoso.com

# View ticket cache location
klist sessions
```

### SPN Management

```powershell
# List SPNs for a computer
setspn -L fileserver

# Query specific SPN
setspn -Q cifs/fileserver.contoso.com

# Check for duplicate SPNs (causes auth failures)
setspn -X

# Add SPN
setspn -A cifs/fileserver.contoso.com fileserver

# Remove SPN
setspn -D cifs/fileserver.contoso.com fileserver
```

### Active Directory Queries

```powershell
# Check service account delegation settings
Get-ADUser -Identity svc_delegation -Properties msDS-AllowedToDelegateTo, TrustedToAuthForDelegation

# Check user account status
Get-ADUser -Identity testuser -Properties Enabled, LockedOut, PasswordExpired, AccountNotDelegated

# Check computer account SPNs
Get-ADComputer -Identity fileserver -Properties servicePrincipalName

# View domain functional level
Get-ADDomain | Select-Object DomainMode, Forest, DomainMode
```

### Network Connectivity

```powershell
# Test domain controller connectivity
Test-Connection -ComputerName DC01 -Count 4

# Test Kerberos port (88)
Test-NetConnection -ComputerName DC01 -Port 88

# Test LDAP port (389)
Test-NetConnection -ComputerName DC01 -Port 389

# Test SMB port (445)
Test-NetConnection -ComputerName fileserver -Port 445

# Test DNS resolution
nslookup fileserver.contoso.com
nslookup DC01.contoso.com
```

### Time Synchronization

```powershell
# Check time sync status
w32tm /query /status

# Check time difference with domain controller
w32tm /stripchart /computer:DC01 /samples:5

# Force time sync
w32tm /resync /force

# View time configuration
w32tm /query /configuration
```

### File Share Access

```powershell
# Test file share access
Test-Path "\\fileserver.contoso.com\TestShare"

# List share permissions
Get-SmbShareAccess -Name TestShare

# List NTFS permissions
Get-Acl "C:\Shares\TestShare" | Format-List

# Test write access
"Test" | Out-File "\\fileserver.contoso.com\TestShare\test.txt"
```

## Delegation Configuration Verification

### Verify Service Account Delegation

```powershell
# Complete delegation verification script
$serviceAccount = "svc_delegation"

Write-Host "=== Delegation Configuration Verification ===" -ForegroundColor Cyan

# Get user object
$user = Get-ADUser -Identity $serviceAccount -Properties msDS-AllowedToDelegateTo, TrustedToAuthForDelegation, AccountNotDelegated

Write-Host "`nService Account: $($user.Name)" -ForegroundColor Yellow

# Check if account can be delegated
if ($user.AccountNotDelegated) {
    Write-Host "[ERROR] Account is marked as 'Account is sensitive and cannot be delegated'" -ForegroundColor Red
    Write-Host "Fix: Set-ADUser -Identity $serviceAccount -AccountNotDelegated `$false" -ForegroundColor Green
} else {
    Write-Host "[OK] Account can be delegated" -ForegroundColor Green
}

# Check protocol transition
if ($user.TrustedToAuthForDelegation) {
    Write-Host "[OK] Protocol transition enabled (TrustedToAuthForDelegation = True)" -ForegroundColor Green
} else {
    Write-Host "[ERROR] Protocol transition not enabled" -ForegroundColor Red
    Write-Host "Fix: Set-ADAccountControl -Identity $serviceAccount -TrustedToAuthForDelegation `$true" -ForegroundColor Green
}

# Check allowed delegation targets
if ($user.'msDS-AllowedToDelegateTo' -and $user.'msDS-AllowedToDelegateTo'.Count -gt 0) {
    Write-Host "[OK] Allowed to delegate to:" -ForegroundColor Green
    $user.'msDS-AllowedToDelegateTo' | ForEach-Object { Write-Host "  - $_" -ForegroundColor White }
} else {
    Write-Host "[ERROR] No delegation targets configured" -ForegroundColor Red
    Write-Host "Fix: Set-ADUser -Identity $serviceAccount -Add @{'msDS-AllowedToDelegateTo' = @('cifs/fileserver.contoso.com')}" -ForegroundColor Green
}

Write-Host "`n=== End Verification ===" -ForegroundColor Cyan
```

### Verify Target User Configuration

```powershell
# Verify target user can be delegated
$targetUser = "testuser"

$user = Get-ADUser -Identity $targetUser -Properties AccountNotDelegated, Enabled, LockedOut

Write-Host "=== Target User Verification ===" -ForegroundColor Cyan
Write-Host "User: $($user.Name)" -ForegroundColor Yellow

if (-not $user.Enabled) {
    Write-Host "[ERROR] User account is disabled" -ForegroundColor Red
} else {
    Write-Host "[OK] User account is enabled" -ForegroundColor Green
}

if ($user.LockedOut) {
    Write-Host "[ERROR] User account is locked out" -ForegroundColor Red
} else {
    Write-Host "[OK] User account is not locked" -ForegroundColor Green
}

if ($user.AccountNotDelegated) {
    Write-Host "[ERROR] User is marked as sensitive and cannot be delegated" -ForegroundColor Red
} else {
    Write-Host "[OK] User can be delegated" -ForegroundColor Green
}
```

## SPN Registration Verification

### Comprehensive SPN Check

```powershell
# Verify SPN registration
$targetServer = "fileserver"
$targetFQDN = "fileserver.contoso.com"
$serviceName = "cifs"

Write-Host "=== SPN Registration Verification ===" -ForegroundColor Cyan

# Check SPNs registered to computer account
$computer = Get-ADComputer -Identity $targetServer -Properties servicePrincipalName
$spns = $computer.servicePrincipalName

Write-Host "`nSPNs registered to $targetServer:" -ForegroundColor Yellow
if ($spns) {
    $spns | ForEach-Object { Write-Host "  - $_" -ForegroundColor White }
} else {
    Write-Host "  [WARNING] No SPNs registered" -ForegroundColor Yellow
}

# Check for required SPNs
$requiredSPNs = @(
    "$serviceName/$targetFQDN",
    "$serviceName/$targetServer"
)

Write-Host "`nRequired SPNs:" -ForegroundColor Yellow
foreach ($spn in $requiredSPNs) {
    if ($spns -contains $spn) {
        Write-Host "  [OK] $spn" -ForegroundColor Green
    } else {
        Write-Host "  [MISSING] $spn" -ForegroundColor Red
        Write-Host "  Fix: setspn -A $spn $targetServer" -ForegroundColor Green
    }
}

# Check for duplicate SPNs
Write-Host "`nChecking for duplicate SPNs..." -ForegroundColor Yellow
$duplicateCheck = setspn -X 2>&1
if ($duplicateCheck -match "found 0 group") {
    Write-Host "[OK] No duplicate SPNs found" -ForegroundColor Green
} else {
    Write-Host "[WARNING] Duplicate SPNs detected:" -ForegroundColor Red
    Write-Host $duplicateCheck -ForegroundColor White
}
```

## Network and Connectivity Issues

### Network Diagnostic Script

```powershell
# Comprehensive network diagnostics
$domainController = "DC01.contoso.com"
$fileServer = "fileserver.contoso.com"

Write-Host "=== Network Connectivity Diagnostics ===" -ForegroundColor Cyan

# Test DNS resolution
Write-Host "`n[1] DNS Resolution:" -ForegroundColor Yellow
try {
    $dcIP = [System.Net.Dns]::GetHostAddresses($domainController)[0].IPAddressToString
    Write-Host "  [OK] $domainController resolves to $dcIP" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Cannot resolve $domainController" -ForegroundColor Red
}

try {
    $fsIP = [System.Net.Dns]::GetHostAddresses($fileServer)[0].IPAddressToString
    Write-Host "  [OK] $fileServer resolves to $fsIP" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Cannot resolve $fileServer" -ForegroundColor Red
}

# Test connectivity
Write-Host "`n[2] Network Connectivity:" -ForegroundColor Yellow
if (Test-Connection -ComputerName $domainController -Count 2 -Quiet) {
    Write-Host "  [OK] Can ping $domainController" -ForegroundColor Green
} else {
    Write-Host "  [ERROR] Cannot ping $domainController" -ForegroundColor Red
}

if (Test-Connection -ComputerName $fileServer -Count 2 -Quiet) {
    Write-Host "  [OK] Can ping $fileServer" -ForegroundColor Green
} else {
    Write-Host "  [ERROR] Cannot ping $fileServer" -ForegroundColor Red
}

# Test ports
Write-Host "`n[3] Port Connectivity:" -ForegroundColor Yellow
$ports = @{
    "Kerberos (88)" = 88
    "LDAP (389)" = 389
    "SMB (445)" = 445
}

foreach ($portName in $ports.Keys) {
    $port = $ports[$portName]
    $result = Test-NetConnection -ComputerName $domainController -Port $port -WarningAction SilentlyContinue
    if ($result.TcpTestSucceeded) {
        Write-Host "  [OK] $portName on $domainController" -ForegroundColor Green
    } else {
        Write-Host "  [ERROR] $portName on $domainController" -ForegroundColor Red
    }
}

# Test time sync
Write-Host "`n[4] Time Synchronization:" -ForegroundColor Yellow
$timeStatus = w32tm /query /status
if ($timeStatus -match "Source:.*$domainController") {
    Write-Host "  [OK] Time synced with $domainController" -ForegroundColor Green
} else {
    Write-Host "  [WARNING] Time may not be synced with domain controller" -ForegroundColor Yellow
}
```

## Event Log Analysis

### View Kerberos Authentication Events

```powershell
# View recent Kerberos events
Get-WinEvent -LogName Security -MaxEvents 100 | 
    Where-Object {$_.Id -in @(4768, 4769, 4770, 4771, 4776)} |
    Select-Object TimeCreated, Id, @{Name='EventType';Expression={
        switch ($_.Id) {
            4768 { "TGT Requested" }
            4769 { "Service Ticket Requested" }
            4770 { "Service Ticket Renewed" }
            4771 { "Pre-auth Failed" }
            4776 { "Credential Validation" }
        }
    }}, Message |
    Format-Table -AutoSize

# Event ID Reference:
# 4768 - Kerberos TGT requested
# 4769 - Kerberos service ticket requested
# 4770 - Kerberos service ticket renewed
# 4771 - Kerberos pre-authentication failed
# 4776 - Domain controller attempted to validate credentials
```

### View Delegation-Specific Events

```powershell
# Filter for delegation events
Get-WinEvent -LogName Security -MaxEvents 500 | 
    Where-Object {$_.Message -match "delegation" -or $_.Message -match "S4U"} |
    Select-Object TimeCreated, Id, Message |
    Format-List
```

### View Application Errors

```powershell
# View application event log
Get-WinEvent -LogName Application -MaxEvents 50 | 
    Where-Object {$_.LevelDisplayName -eq "Error"} |
    Select-Object TimeCreated, ProviderName, Message |
    Format-List
```

## Advanced Troubleshooting

### Enable Kerberos Logging

```powershell
# Enable Kerberos event logging
reg add "HKLM\SYSTEM\CurrentControlSet\Control\Lsa\Kerberos\Parameters" /v LogLevel /t REG_DWORD /d 1 /f

# Restart computer for changes to take effect
Restart-Computer -Force

# View Kerberos logs
Get-WinEvent -LogName System | Where-Object {$_.ProviderName -eq "Kerberos"}
```

### Network Trace

```powershell
# Capture network trace (requires admin)
netsh trace start capture=yes tracefile=C:\Temp\kerberos_trace.etl

# Reproduce the issue

# Stop trace
netsh trace stop

# Analyze with Network Monitor or Wireshark
```

### Process Monitor

Use Process Monitor (procmon.exe) to trace:
- File access attempts
- Registry access
- Network activity
- Process creation

Filter by process name: `KerberosConstrainedDelegation.exe` or `FileShareWriter.exe`

### Kerberos Ticket Decoder

```powershell
# Export ticket for analysis
klist tickets > C:\Temp\tickets.txt

# Decode ticket (requires klist with /decode option or third-party tools)
# Look for:
# - Ticket expiration time
# - Encryption type (should be AES256)
# - Forwardable flag
# - Service name
```

## Getting Help

If you've tried all troubleshooting steps and still have issues:

1. **Collect Diagnostic Information:**
   - Application logs
   - Event Viewer logs (Security, Application, System)
   - Configuration files (remove passwords)
   - Output of diagnostic commands

2. **Check Documentation:**
   - [SETUP_GUIDE.md](SETUP_GUIDE.md)
   - [CONFIGURATION_EXAMPLES.md](CONFIGURATION_EXAMPLES.md)
   - Microsoft Kerberos documentation

3. **Common Resources:**
   - Microsoft Docs: Kerberos Constrained Delegation
   - Windows Server Security documentation
   - Active Directory troubleshooting guides

## Quick Reference

### Exit Codes

| Exit Code | Meaning |
|-----------|---------|
| 0 | Success |
| 1 | Configuration validation failed |
| 2 | Delegation configuration invalid |
| 3 | Process execution failed |
| 4 | Kerberos error |
| 5 | General error |

### FileShareWriter Exit Codes

| Exit Code | Meaning |
|-----------|---------|
| 0 | Success |
| 1 | Invalid arguments |
| 2 | Invalid UNC path |
| 5 | Access denied |
| 6 | I/O error |
| 7 | General error |

### Win32 Error Codes

| Error Code | Constant | Meaning |
|------------|----------|---------|
| 5 | ERROR_ACCESS_DENIED | Access denied |
| 50 | ERROR_NOT_SUPPORTED | Operation not supported |
| 1326 | ERROR_LOGON_FAILURE | Logon failure |
| 1317 | ERROR_NO_SUCH_USER | User does not exist |
| 1398 | ERROR_MUTUAL_AUTH_FAILED | Mutual authentication failed |

### NTSTATUS Codes

| Status Code | Meaning |
|-------------|---------|
| 0x00000000 | STATUS_SUCCESS |
| 0xC000006D | STATUS_LOGON_FAILURE |
| 0xC0000064 | STATUS_NO_SUCH_USER |
| 0xC00000DC | STATUS_INVALID_SERVER_STATE |
| 0xC0000133 | STATUS_TIME_DIFFERENCE_AT_DC |
