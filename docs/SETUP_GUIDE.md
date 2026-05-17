# Kerberos Constrained Delegation Setup Guide

This guide provides step-by-step instructions for setting up Active Directory, configuring constrained delegation, and deploying the Kerberos Constrained Delegation application.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Active Directory Configuration](#active-directory-configuration)
3. [Service Account Creation](#service-account-creation)
4. [Constrained Delegation Configuration](#constrained-delegation-configuration)
5. [SPN Registration](#spn-registration)
6. [File Share Setup](#file-share-setup)
7. [Application Configuration](#application-configuration)
8. [Verification](#verification)

## Prerequisites

Before beginning setup, ensure you have:

- **Domain Administrator Access**: Required for creating service accounts and configuring delegation
- **Windows Server**: Windows Server 2008 R2 or higher with Active Directory Domain Services
- **Domain Functional Level**: Windows Server 2008 or higher
- **File Server**: Windows file server for testing delegation
- **.NET 9.0 SDK**: Installed on the machine running the application
- **PowerShell**: Version 5.1 or higher with Active Directory module

### Verify Prerequisites

```powershell
# Check domain functional level
Get-ADDomain | Select-Object DomainMode

# Check Active Directory module
Get-Module -ListAvailable ActiveDirectory

# Check .NET SDK
dotnet --version

# Verify domain membership
(Get-WmiObject Win32_ComputerSystem).PartOfDomain
```

## Active Directory Configuration

### 1. Verify Kerberos is Enabled

Kerberos should be enabled by default in Active Directory. Verify with:

```powershell
# Check Kerberos policy
Get-ADDefaultDomainPasswordPolicy | Select-Object MaxPasswordAge, MinPasswordAge, LockoutDuration

# Verify KDC service is running on domain controllers
Get-Service -Name kdc -ComputerName DC01
```

### 2. Configure Time Synchronization

Kerberos requires time synchronization within 5 minutes between all systems.

```powershell
# On domain controller (time source)
w32tm /config /manualpeerlist:"time.windows.com" /syncfromflags:manual /reliable:yes /update
w32tm /resync

# On client machines
w32tm /config /syncfromflags:domhier /update
w32tm /resync

# Verify time sync
w32tm /query /status
```

### 3. Configure DNS

Ensure DNS is properly configured for SPN resolution:

```powershell
# Verify DNS resolution
nslookup fileserver.contoso.com
nslookup dc01.contoso.com

# Test reverse DNS
nslookup 192.168.1.10

# Verify SRV records for Kerberos
nslookup -type=SRV _kerberos._tcp.contoso.com
```

## Service Account Creation

### 1. Create Service Account

Create a dedicated service account for the application:

```powershell
# Create service account
New-ADUser -Name "svc_delegation" `
    -SamAccountName "svc_delegation" `
    -UserPrincipalName "svc_delegation@contoso.com" `
    -AccountPassword (ConvertTo-SecureString "ComplexP@ssw0rd123!" -AsPlainText -Force) `
    -Enabled $true `
    -PasswordNeverExpires $true `
    -CannotChangePassword $false `
    -Description "Service account for Kerberos constrained delegation application"

# Verify account creation
Get-ADUser -Identity svc_delegation
```

### 2. Configure Service Account Properties

```powershell
# Set account to not require Kerberos pre-authentication (if needed)
Set-ADAccountControl -Identity svc_delegation -DoesNotRequirePreAuth $false

# Ensure account is not marked as sensitive
Set-ADUser -Identity svc_delegation -AccountNotDelegated $false

# Set password to never expire (for service accounts)
Set-ADUser -Identity svc_delegation -PasswordNeverExpires $true
```

### 3. Grant Necessary Permissions

The service account needs specific permissions:

```powershell
# Grant "Log on as a service" right (via Group Policy or local security policy)
# This is typically done through Group Policy Management Console

# For testing, you can grant "Act as part of the operating system" (not recommended for production)
# This requires manual configuration in Local Security Policy
```

**Note**: For production environments, use Group Policy to grant "Log on as a service" right to the service account.

## Constrained Delegation Configuration

### 1. Configure Constrained Delegation via GUI

1. Open **Active Directory Users and Computers** (dsa.msc)
2. Navigate to the service account (`svc_delegation`)
3. Right-click and select **Properties**
4. Go to the **Delegation** tab

   ![Delegation Tab](images/delegation-tab.png)

5. Select **"Trust this user for delegation to specified services only"**
6. Select **"Use any authentication protocol"** (allows protocol transition)

   ![Delegation Options](images/delegation-options.png)

7. Click **Add** to add services
8. Click **Users or Computers**
9. Enter the target server name (e.g., `fileserver`)
10. Click **OK**
11. Select the service types to delegate to (e.g., `cifs` for file shares)

    ![Service Selection](images/service-selection.png)

12. Click **OK** to save

### 2. Configure Constrained Delegation via PowerShell

```powershell
# Get the service account
$serviceAccount = Get-ADUser -Identity svc_delegation

# Define target SPNs for delegation
$targetSPNs = @(
    "cifs/fileserver.contoso.com",
    "cifs/fileserver"
)

# Configure constrained delegation
Set-ADUser -Identity svc_delegation -Add @{
    'msDS-AllowedToDelegateTo' = $targetSPNs
}

# Enable protocol transition (allows S4U2Self)
Set-ADAccountControl -Identity svc_delegation -TrustedToAuthForDelegation $true

# Verify configuration
Get-ADUser -Identity svc_delegation -Properties msDS-AllowedToDelegateTo, TrustedToAuthForDelegation | 
    Select-Object Name, TrustedToAuthForDelegation, @{
        Name='AllowedToDelegateTo'
        Expression={$_.'msDS-AllowedToDelegateTo'}
    }
```

### 3. Verify Delegation Configuration

```powershell
# Check delegation settings
$user = Get-ADUser -Identity svc_delegation -Properties msDS-AllowedToDelegateTo, TrustedToAuthForDelegation

Write-Host "Service Account: $($user.Name)"
Write-Host "Trusted for Delegation: $($user.TrustedToAuthForDelegation)"
Write-Host "Allowed to Delegate To:"
$user.'msDS-AllowedToDelegateTo' | ForEach-Object { Write-Host "  - $_" }
```

## SPN Registration

Service Principal Names (SPNs) must be registered for the target services.

### 1. Register File Share SPN

```powershell
# Register CIFS SPN for file server (FQDN)
setspn -A cifs/fileserver.contoso.com fileserver

# Register CIFS SPN for file server (NetBIOS name)
setspn -A cifs/fileserver fileserver

# Verify SPN registration
setspn -L fileserver
```

### 2. Register SQL Server SPN (if applicable)

```powershell
# Register MSSQLSvc SPN for SQL Server
setspn -A MSSQLSvc/sqlserver.contoso.com:1433 sqlserver

# Verify SPN registration
setspn -L sqlserver
```

### 3. Check for Duplicate SPNs

Duplicate SPNs cause authentication failures:

```powershell
# Check for duplicate SPNs
setspn -X

# If duplicates found, remove them
setspn -D cifs/fileserver.contoso.com duplicateAccount
```

### 4. Verify SPN Resolution

```powershell
# Query SPN from Active Directory
Get-ADObject -Filter {servicePrincipalName -eq "cifs/fileserver.contoso.com"} -Properties servicePrincipalName

# Test SPN resolution
setspn -Q cifs/fileserver.contoso.com
```

## File Share Setup

### 1. Create File Share

On the file server (fileserver.contoso.com):

```powershell
# Create directory
New-Item -Path "C:\Shares\TestShare" -ItemType Directory

# Create SMB share
New-SmbShare -Name "TestShare" `
    -Path "C:\Shares\TestShare" `
    -FullAccess "CONTOSO\Domain Admins" `
    -ChangeAccess "CONTOSO\Domain Users" `
    -Description "Test share for Kerberos delegation"

# Verify share creation
Get-SmbShare -Name TestShare
```

### 2. Configure NTFS Permissions

```powershell
# Grant permissions to test users
$acl = Get-Acl "C:\Shares\TestShare"

# Add permission for test user
$permission = "CONTOSO\testuser", "Modify", "ContainerInherit,ObjectInherit", "None", "Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)

# Apply ACL
Set-Acl "C:\Shares\TestShare" $acl

# Verify permissions
Get-Acl "C:\Shares\TestShare" | Format-List
```

### 3. Configure SMB Settings

```powershell
# Enable SMB signing (recommended for security)
Set-SmbServerConfiguration -RequireSecuritySignature $true -Force

# Verify SMB configuration
Get-SmbServerConfiguration | Select-Object RequireSecuritySignature, EnableSecuritySignature

# Test share access
Test-Path "\\fileserver.contoso.com\TestShare"
```

### 4. Configure Firewall Rules

Ensure firewall allows SMB traffic:

```powershell
# Enable SMB firewall rules
Enable-NetFirewallRule -DisplayGroup "File and Printer Sharing"

# Verify firewall rules
Get-NetFirewallRule -DisplayGroup "File and Printer Sharing" | Where-Object {$_.Enabled -eq $true}
```

## Application Configuration

### 1. Build the Application

```powershell
# Navigate to project directory
cd C:\Projects\offline-jobs

# Restore dependencies
dotnet restore

# Build the solution
dotnet build -c Release

# Publish the application
dotnet publish src/KerberosConstrainedDelegation/KerberosConstrainedDelegation.csproj `
    -c Release `
    -o C:\Apps\KerberosDelegation
```

### 2. Configure appsettings.json

Create or edit `appsettings.json`:

```json
{
  "ServiceAccount": {
    "Username": "svc_delegation",
    "Domain": "CONTOSO",
    "Password": "ComplexP@ssw0rd123!"
  },
  "Delegation": {
    "TargetUsername": "testuser@contoso.com",
    "TargetServicePrincipalName": "cifs/fileserver.contoso.com",
    "ExternalExecutablePath": "C:\\Apps\\KerberosDelegation\\FileShareWriter.exe",
    "FileSharePath": "\\\\fileserver.contoso.com\\TestShare\\test.txt",
    "TimeoutMs": 30000
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  }
}
```

### 3. Secure Configuration File

For production, use encrypted configuration or Azure Key Vault:

```powershell
# Option 1: Use Windows Credential Manager
cmdkey /add:KerberosDelegation /user:svc_delegation /pass:ComplexP@ssw0rd123!

# Option 2: Use DPAPI to encrypt configuration
# (Requires custom implementation in the application)

# Option 3: Use Azure Key Vault
# (Requires Azure subscription and Key Vault setup)
```

### 4. Deploy FileShareWriter

Ensure FileShareWriter.exe is in the correct location:

```powershell
# Copy FileShareWriter to deployment directory
Copy-Item "src\FileShareWriter\bin\Release\net9.0\FileShareWriter.exe" `
    -Destination "C:\Apps\KerberosDelegation\" `
    -Force

# Verify file exists
Test-Path "C:\Apps\KerberosDelegation\FileShareWriter.exe"
```

## Verification

### 1. Verify Service Account Authentication

```powershell
# Test service account login
$cred = Get-Credential -UserName "CONTOSO\svc_delegation" -Message "Enter service account password"

# Attempt to access a network resource
Get-ChildItem "\\fileserver.contoso.com\TestShare" -Credential $cred
```

### 2. Verify Delegation Configuration

```powershell
# Check delegation settings
Get-ADUser -Identity svc_delegation -Properties msDS-AllowedToDelegateTo | 
    Select-Object -ExpandProperty msDS-AllowedToDelegateTo

# Expected output:
# cifs/fileserver.contoso.com
# cifs/fileserver
```

### 3. Verify SPN Registration

```powershell
# List SPNs for file server
setspn -L fileserver

# Expected output should include:
# cifs/fileserver.contoso.com
# cifs/fileserver
```

### 4. Test File Share Access

```powershell
# Test as target user
$testCred = Get-Credential -UserName "CONTOSO\testuser" -Message "Enter test user password"

# Create test file
"Test content" | Out-File "\\fileserver.contoso.com\TestShare\test.txt" -Credential $testCred

# Verify file was created
Get-Content "\\fileserver.contoso.com\TestShare\test.txt" -Credential $testCred
```

### 5. Run the Application

```powershell
# Navigate to application directory
cd C:\Apps\KerberosDelegation

# Run the application
dotnet KerberosConstrainedDelegation.dll

# Expected output:
# [INFO] Loading configuration...
# [INFO] Configuration validated successfully
# [INFO] Obtaining delegated token for user: testuser@contoso.com
# [INFO] Successfully obtained delegated token
# [INFO] Process exit code: 0
```

### 6. Verify Kerberos Tickets

```powershell
# View current Kerberos tickets
klist

# Purge tickets (if needed for testing)
klist purge

# Request new ticket
klist get cifs/fileserver.contoso.com
```

### 7. Check Event Logs

Monitor Windows Event Logs for authentication events:

```powershell
# Check Security log for Kerberos events
Get-WinEvent -LogName Security -MaxEvents 50 | 
    Where-Object {$_.Id -in @(4768, 4769, 4770, 4771, 4776)} |
    Format-Table TimeCreated, Id, Message -AutoSize

# Event IDs:
# 4768 - Kerberos TGT requested
# 4769 - Kerberos service ticket requested
# 4770 - Kerberos service ticket renewed
# 4771 - Kerberos pre-authentication failed
# 4776 - Domain controller attempted to validate credentials
```

## Troubleshooting Setup Issues

### Issue: "Delegation tab not visible"

**Cause**: Account does not have an SPN registered or you don't have sufficient permissions.

**Solution**:
```powershell
# Register a dummy SPN to enable delegation tab
setspn -A HTTP/dummy svc_delegation

# Refresh Active Directory Users and Computers
# The Delegation tab should now appear
```

### Issue: "Cannot find object" when adding services

**Cause**: Target server does not have SPNs registered.

**Solution**:
```powershell
# Register SPNs for target server first
setspn -A cifs/fileserver.contoso.com fileserver
setspn -A cifs/fileserver fileserver
```

### Issue: Time synchronization errors

**Cause**: Time difference exceeds 5 minutes between systems.

**Solution**:
```powershell
# Sync time on all systems
w32tm /resync /force

# Verify time
w32tm /query /status
```

## Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                     Active Directory Domain                      │
│                                                                   │
│  ┌──────────────┐         ┌──────────────┐                      │
│  │   Domain     │         │   Service    │                      │
│  │  Controller  │◄────────┤   Account    │                      │
│  │   (KDC)      │         │ svc_delegation│                      │
│  └──────────────┘         └──────────────┘                      │
│         │                         │                              │
│         │ Kerberos Tickets        │ Constrained Delegation       │
│         │                         │ Configured for:              │
│         ▼                         │ - cifs/fileserver.contoso.com│
│  ┌──────────────┐                │                              │
│  │  Application │◄───────────────┘                              │
│  │    Server    │                                                │
│  └──────────────┘                                                │
│         │                                                        │
│         │ S4U2Self + S4U2Proxy                                   │
│         │                                                        │
│         ▼                                                        │
│  ┌──────────────┐         ┌──────────────┐                      │
│  │ FileShareWriter│───────►│ File Server  │                      │
│  │   Process    │         │  (fileserver) │                      │
│  │ (Delegated   │         │               │                      │
│  │ Credentials) │         │ SPN: cifs/... │                      │
│  └──────────────┘         └──────────────┘                      │
│                                   │                              │
│                                   ▼                              │
│                           ┌──────────────┐                       │
│                           │  File Share  │                       │
│                           │  \\fileserver\│                       │
│                           │   TestShare  │                       │
│                           └──────────────┘                       │
└─────────────────────────────────────────────────────────────────┘
```

## Next Steps

After completing setup:

1. Review [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for common issues
2. Review [CONFIGURATION_EXAMPLES.md](CONFIGURATION_EXAMPLES.md) for different scenarios
3. Run the application and verify successful delegation
4. Monitor Event Logs for any authentication issues
5. Implement additional security measures for production deployment

## Security Best Practices

1. **Use Strong Passwords**: Service account passwords should be complex and rotated regularly
2. **Limit Delegation Scope**: Only delegate to specific SPNs that are required
3. **Monitor Delegation Usage**: Review Event Logs regularly for suspicious activity
4. **Use Constrained Delegation**: Never use unconstrained delegation
5. **Protect Configuration**: Encrypt configuration files or use secure credential storage
6. **Least Privilege**: Grant minimum permissions required for service account
7. **Regular Audits**: Periodically review delegation configuration and access logs

## References

- [Microsoft: Kerberos Constrained Delegation Overview](https://docs.microsoft.com/en-us/windows-server/security/kerberos/kerberos-constrained-delegation-overview)
- [Microsoft: Service Principal Names (SPNs)](https://docs.microsoft.com/en-us/windows/win32/ad/service-principal-names)
- [Microsoft: How to Configure Constrained Delegation](https://docs.microsoft.com/en-us/windows-server/security/kerberos/kerberos-constrained-delegation-overview)
