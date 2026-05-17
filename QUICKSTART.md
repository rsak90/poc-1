# Quick Start Guide - Running the Kerberos Constrained Delegation Application

This guide provides step-by-step instructions to get the application running in your environment.

## Table of Contents

1. [Prerequisites Check](#prerequisites-check)
2. [Quick Setup (15 minutes)](#quick-setup-15-minutes)
3. [Configuration](#configuration)
4. [Running the Application](#running-the-application)
5. [Verification](#verification)
6. [Troubleshooting](#troubleshooting)

---

## Prerequisites Check

Before starting, verify you have:

### Required
- ✅ Windows Server 2008 R2 or higher (or Windows 7+)
- ✅ .NET 9.0 SDK installed
- ✅ Active Directory domain membership
- ✅ Domain Administrator access (for setup)
- ✅ PowerShell 5.1 or higher

### Verify Prerequisites

```powershell
# Check .NET SDK
dotnet --version
# Expected: 9.0.x or higher

# Check domain membership
(Get-WmiObject Win32_ComputerSystem).PartOfDomain
# Expected: True

# Check PowerShell version
$PSVersionTable.PSVersion
# Expected: 5.1 or higher

# Check if you have AD module
Get-Module -ListAvailable ActiveDirectory
# Expected: Module should be listed
```

---

## Quick Setup (15 minutes)

### Step 1: Create Service Account (2 minutes)

Open PowerShell as Administrator and run:

```powershell
# Replace with your domain name
$domain = "CONTOSO"
$domainFQDN = "contoso.com"

# Create service account
New-ADUser -Name "svc_delegation" `
    -SamAccountName "svc_delegation" `
    -UserPrincipalName "svc_delegation@$domainFQDN" `
    -AccountPassword (Read-Host "Enter password for service account" -AsSecureString) `
    -Enabled $true `
    -PasswordNeverExpires $true `
    -Description "Service account for Kerberos constrained delegation"

Write-Host "✓ Service account created" -ForegroundColor Green
```

### Step 2: Create Test User (1 minute)

```powershell
# Create test user (if you don't have one)
New-ADUser -Name "testuser" `
    -SamAccountName "testuser" `
    -UserPrincipalName "testuser@$domainFQDN" `
    -AccountPassword (Read-Host "Enter password for test user" -AsSecureString) `
    -Enabled $true

Write-Host "✓ Test user created" -ForegroundColor Green
```

### Step 3: Register SPNs (2 minutes)

```powershell
# Replace with your file server name
$fileServer = "fileserver"
$fileServerFQDN = "fileserver.$domainFQDN"

# Register SPNs for file server
setspn -A cifs/$fileServerFQDN $fileServer
setspn -A cifs/$fileServer $fileServer

# Verify registration
setspn -L $fileServer

Write-Host "✓ SPNs registered" -ForegroundColor Green
```

### Step 4: Configure Constrained Delegation (3 minutes)

```powershell
# Configure delegation for service account
Set-ADUser -Identity svc_delegation -Add @{
    'msDS-AllowedToDelegateTo' = @(
        "cifs/$fileServerFQDN",
        "cifs/$fileServer"
    )
}

# Enable protocol transition (required for S4U2Self)
Set-ADAccountControl -Identity svc_delegation -TrustedToAuthForDelegation $true

# Verify configuration
Get-ADUser -Identity svc_delegation -Properties msDS-AllowedToDelegateTo, TrustedToAuthForDelegation | 
    Select-Object Name, TrustedToAuthForDelegation, @{
        Name='AllowedToDelegateTo'
        Expression={$_.'msDS-AllowedToDelegateTo'}
    }

Write-Host "✓ Delegation configured" -ForegroundColor Green
```

### Step 5: Create Test File Share (3 minutes)

On your file server, run:

```powershell
# Create directory
New-Item -Path "C:\Shares\TestShare" -ItemType Directory -Force

# Create SMB share
New-SmbShare -Name "TestShare" `
    -Path "C:\Shares\TestShare" `
    -FullAccess "Everyone" `
    -Description "Test share for Kerberos delegation"

# Set NTFS permissions
$acl = Get-Acl "C:\Shares\TestShare"
$permission = "Everyone", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)
Set-Acl "C:\Shares\TestShare" $acl

Write-Host "✓ File share created" -ForegroundColor Green
```

### Step 6: Build the Application (2 minutes)

```powershell
# Navigate to project directory
cd C:\Users\USER\OneDrive\Desktop\offline-jobs

# Build the solution
dotnet build KerberosConstrainedDelegation.sln -c Release

Write-Host "✓ Application built" -ForegroundColor Green
```

### Step 7: Configure Application (2 minutes)

Edit `src\KerberosConstrainedDelegation\appsettings.json`:

```json
{
  "ServiceAccount": {
    "Username": "svc_delegation",
    "Domain": "CONTOSO",
    "Password": "YourActualPassword"
  },
  "TargetUser": {
    "Username": "testuser@contoso.com"
  },
  "TargetService": {
    "Spn": "cifs/fileserver.contoso.com"
  },
  "ExternalExecutable": {
    "Path": "C:\\Users\\USER\\OneDrive\\Desktop\\offline-jobs\\src\\FileShareWriter\\bin\\Release\\net9.0\\FileShareWriter.exe"
  },
  "FileShare": {
    "Path": "\\\\fileserver.contoso.com\\TestShare\\test.txt"
  },
  "Execution": {
    "TimeoutSeconds": 30
  }
}
```

**Important**: Replace these values:
- `Password`: Your actual service account password
- `Domain`: Your actual domain name
- `Username`: Your actual test user (UPN format)
- `Spn`: Your actual file server FQDN
- `Path` (ExternalExecutable): Full path to FileShareWriter.exe
- `Path` (FileShare): Your actual UNC path

---

## Configuration

### Option 1: Using appsettings.json (Recommended)

1. Navigate to the project directory:
   ```powershell
   cd C:\Users\USER\OneDrive\Desktop\offline-jobs\src\KerberosConstrainedDelegation
   ```

2. Edit `appsettings.json` with your values (see Step 7 above)

3. Copy the file to the build output:
   ```powershell
   Copy-Item appsettings.json bin\Release\net9.0\ -Force
   ```

### Option 2: Using Command-Line Arguments

You can override configuration using command-line arguments:

```powershell
dotnet run --project src\KerberosConstrainedDelegation\KerberosConstrainedDelegation.csproj -- `
  ServiceAccount:Username=svc_delegation `
  ServiceAccount:Domain=CONTOSO `
  ServiceAccount:Password=YourPassword `
  TargetUser:Username=testuser@contoso.com `
  TargetService:Spn=cifs/fileserver.contoso.com `
  ExternalExecutable:Path=C:\Path\To\FileShareWriter.exe `
  FileShare:Path=\\fileserver\TestShare\test.txt
```

---

## Running the Application

### Method 1: Run from Source

```powershell
cd C:\Users\USER\OneDrive\Desktop\offline-jobs

# Run with configuration file
dotnet run --project src\KerberosConstrainedDelegation\KerberosConstrainedDelegation.csproj -c Release
```

### Method 2: Run from Build Output

```powershell
cd C:\Users\USER\OneDrive\Desktop\offline-jobs\src\KerberosConstrainedDelegation\bin\Release\net9.0

# Run the executable
dotnet KerberosConstrainedDelegation.dll
```

### Method 3: Run with Command-Line Arguments

```powershell
cd C:\Users\USER\OneDrive\Desktop\offline-jobs

dotnet run --project src\KerberosConstrainedDelegation\KerberosConstrainedDelegation.csproj -c Release -- `
  ServiceAccount:Username=svc_delegation `
  ServiceAccount:Domain=CONTOSO `
  ServiceAccount:Password=YourPassword `
  TargetUser:Username=testuser@contoso.com `
  TargetService:Spn=cifs/fileserver.contoso.com `
  ExternalExecutable:Path=C:\Users\USER\OneDrive\Desktop\offline-jobs\src\FileShareWriter\bin\Release\net9.0\FileShareWriter.exe `
  FileShare:Path=\\fileserver.contoso.com\TestShare\test.txt
```

---

## Verification

### Expected Output (Success)

```
[2026-05-14 17:35:30] Kerberos Constrained Delegation Application
[2026-05-14 17:35:30] ============================================

[2026-05-14 17:35:30] Loading configuration...
[2026-05-14 17:35:30] Validating configuration...
[2026-05-14 17:35:30] Configuration validated successfully

[2026-05-14 17:35:30] Initializing Kerberos Token Manager...
[2026-05-14 17:35:30] Service Account: CONTOSO\svc_delegation

[2026-05-14 17:35:30] Validating delegation configuration...
[2026-05-14 17:35:31] Delegation configuration is valid

[2026-05-14 17:35:31] Obtaining delegated token...
[2026-05-14 17:35:31] Target User: testuser@contoso.com
[2026-05-14 17:35:31] Target SPN: cifs/fileserver.contoso.com

[2026-05-14 17:35:32] Successfully obtained delegated token
[2026-05-14 17:35:32] Token represents: CONTOSO\testuser

[2026-05-14 17:35:32] Spawning external process...
[2026-05-14 17:35:32] Executable: C:\...\FileShareWriter.exe
[2026-05-14 17:35:32] Arguments: "\\fileserver.contoso.com\TestShare\test.txt" "Test content"

[2026-05-14 17:35:33] Process completed successfully
[2026-05-14 17:35:33] Exit Code: 0
[2026-05-14 17:35:33] Execution Time: 1.23s

[2026-05-14 17:35:33] Process Output:
Running as: CONTOSO\testuser
Authentication type: Kerberos
Is authenticated: True
SID: S-1-5-21-...
Successfully wrote to: \\fileserver.contoso.com\TestShare\test.txt

[2026-05-14 17:35:33] ============================================
[2026-05-14 17:35:33] Delegation test completed successfully!
```

### Verify File Was Created

```powershell
# Check if file exists
Test-Path "\\fileserver.contoso.com\TestShare\test.txt"

# Read file content
Get-Content "\\fileserver.contoso.com\TestShare\test.txt"
```

### Check Kerberos Tickets

```powershell
# View current tickets
klist

# Look for tickets to the file server
klist | Select-String "cifs/fileserver"
```

---

## Troubleshooting

### Error: "Configuration validation failed"

**Problem**: Configuration file not found or invalid

**Solution**:
```powershell
# Verify file exists
Test-Path "src\KerberosConstrainedDelegation\bin\Release\net9.0\appsettings.json"

# Copy from source if missing
Copy-Item src\KerberosConstrainedDelegation\appsettings.json `
          src\KerberosConstrainedDelegation\bin\Release\net9.0\ -Force
```

### Error: "Service authentication failed"

**Problem**: Service account credentials are incorrect

**Solution**:
```powershell
# Test credentials manually
$cred = Get-Credential -UserName "CONTOSO\svc_delegation"
Get-ADUser -Identity svc_delegation -Credential $cred

# Reset password if needed
Set-ADAccountPassword -Identity svc_delegation -Reset
```

### Error: "Service not trusted for delegation"

**Problem**: Delegation not configured

**Solution**:
```powershell
# Verify delegation configuration
Get-ADUser -Identity svc_delegation -Properties msDS-AllowedToDelegateTo | 
    Select-Object -ExpandProperty msDS-AllowedToDelegateTo

# If empty, configure delegation (see Step 4)
```

### Error: "User not found"

**Problem**: Target user doesn't exist or format is wrong

**Solution**:
```powershell
# Verify user exists
Get-ADUser -Identity testuser

# Use correct format in config:
# - UPN: testuser@contoso.com
# - DOMAIN\username: CONTOSO\testuser
```

### Error: "Access denied" (Exit Code 5)

**Problem**: User doesn't have permissions on file share

**Solution**:
```powershell
# Grant permissions
Grant-SmbShareAccess -Name TestShare -AccountName "CONTOSO\testuser" -AccessRight Change -Force

# Verify permissions
Get-SmbShareAccess -Name TestShare
```

### Error: "FileShareWriter.exe not found"

**Problem**: Executable path is incorrect

**Solution**:
```powershell
# Find the correct path
Get-ChildItem -Path "src\FileShareWriter\bin\Release\net9.0" -Filter "FileShareWriter.exe" -Recurse

# Update appsettings.json with the full path
```

---

## Quick Diagnostic Script

Run this script to check your configuration:

```powershell
Write-Host "=== Kerberos Delegation Diagnostic ===" -ForegroundColor Cyan

# 1. Check service account
Write-Host "`n[1] Checking service account..." -ForegroundColor Yellow
try {
    $svcAccount = Get-ADUser -Identity svc_delegation -Properties msDS-AllowedToDelegateTo, TrustedToAuthForDelegation
    Write-Host "  ✓ Service account exists" -ForegroundColor Green
    
    if ($svcAccount.TrustedToAuthForDelegation) {
        Write-Host "  ✓ Protocol transition enabled" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Protocol transition NOT enabled" -ForegroundColor Red
    }
    
    if ($svcAccount.'msDS-AllowedToDelegateTo') {
        Write-Host "  ✓ Delegation targets configured:" -ForegroundColor Green
        $svcAccount.'msDS-AllowedToDelegateTo' | ForEach-Object { Write-Host "    - $_" }
    } else {
        Write-Host "  ✗ No delegation targets configured" -ForegroundColor Red
    }
} catch {
    Write-Host "  ✗ Service account not found" -ForegroundColor Red
}

# 2. Check test user
Write-Host "`n[2] Checking test user..." -ForegroundColor Yellow
try {
    $testUser = Get-ADUser -Identity testuser -Properties AccountNotDelegated
    Write-Host "  ✓ Test user exists" -ForegroundColor Green
    
    if (-not $testUser.AccountNotDelegated) {
        Write-Host "  ✓ User can be delegated" -ForegroundColor Green
    } else {
        Write-Host "  ✗ User marked as sensitive (cannot be delegated)" -ForegroundColor Red
    }
} catch {
    Write-Host "  ✗ Test user not found" -ForegroundColor Red
}

# 3. Check SPNs
Write-Host "`n[3] Checking SPN registration..." -ForegroundColor Yellow
$spnCheck = setspn -Q cifs/fileserver.contoso.com 2>&1
if ($spnCheck -match "Existing SPN found") {
    Write-Host "  ✓ SPN registered" -ForegroundColor Green
} else {
    Write-Host "  ✗ SPN not found" -ForegroundColor Red
}

# 4. Check file share
Write-Host "`n[4] Checking file share..." -ForegroundColor Yellow
if (Test-Path "\\fileserver.contoso.com\TestShare") {
    Write-Host "  ✓ File share accessible" -ForegroundColor Green
} else {
    Write-Host "  ✗ File share not accessible" -ForegroundColor Red
}

# 5. Check executable
Write-Host "`n[5] Checking FileShareWriter..." -ForegroundColor Yellow
$exePath = "C:\Users\USER\OneDrive\Desktop\offline-jobs\src\FileShareWriter\bin\Release\net9.0\FileShareWriter.exe"
if (Test-Path $exePath) {
    Write-Host "  ✓ Executable exists" -ForegroundColor Green
} else {
    Write-Host "  ✗ Executable not found" -ForegroundColor Red
}

Write-Host "`n=== Diagnostic Complete ===" -ForegroundColor Cyan
```

---

## Next Steps

After successfully running the application:

1. **Review Logs**: Check Event Viewer for Kerberos events (Event IDs: 4768, 4769)
2. **Test Different Users**: Try with different target users
3. **Test Different SPNs**: Try delegating to SQL Server or other services
4. **Production Setup**: Review security hardening in `docs/SETUP_GUIDE.md`

---

## Additional Resources

- **Detailed Setup**: See `docs/SETUP_GUIDE.md` for comprehensive Active Directory configuration
- **Troubleshooting**: See `docs/TROUBLESHOOTING.md` for detailed error solutions
- **Configuration Examples**: See `docs/CONFIGURATION_EXAMPLES.md` for different scenarios
- **README**: See `README.md` for project overview and architecture

---

## Support

If you encounter issues:

1. Run the diagnostic script above
2. Check `docs/TROUBLESHOOTING.md` for your specific error
3. Review Event Viewer logs (Security and Application)
4. Verify all prerequisites are met

---

**Last Updated**: May 14, 2026
