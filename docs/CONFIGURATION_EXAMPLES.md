# Configuration Examples

This document provides configuration examples for different use cases and scenarios when using the Kerberos Constrained Delegation application.

## Table of Contents

1. [Basic Configuration](#basic-configuration)
2. [File Share Access Scenarios](#file-share-access-scenarios)
3. [Database Access Scenarios](#database-access-scenarios)
4. [Web Application Scenarios](#web-application-scenarios)
5. [Command-Line Usage Examples](#command-line-usage-examples)
6. [Advanced Configuration](#advanced-configuration)
7. [Production Configuration](#production-configuration)

## Basic Configuration

### Minimal Configuration (appsettings.json)

```json
{
  "ServiceAccount": {
    "Username": "svc_delegation",
    "Domain": "CONTOSO",
    "Password": "YourSecurePassword"
  },
  "Delegation": {
    "TargetUsername": "testuser@contoso.com",
    "TargetServicePrincipalName": "cifs/fileserver.contoso.com",
    "ExternalExecutablePath": "C:\\Apps\\FileShareWriter.exe",
    "FileSharePath": "\\\\fileserver.contoso.com\\share\\test.txt"
  }
}
```

### Full Configuration with All Options

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
      "Microsoft": "Warning",
      "System": "Warning"
    },
    "Console": {
      "FormatterName": "simple",
      "FormatterOptions": {
        "SingleLine": true,
        "IncludeScopes": true,
        "TimestampFormat": "yyyy-MM-dd HH:mm:ss "
      }
    }
  }
}
```

## File Share Access Scenarios

### Scenario 1: Single File Share Access

**Use Case**: Web application needs to save user-uploaded documents to a file share.

**Configuration**:
```json
{
  "ServiceAccount": {
    "Username": "svc_webapp",
    "Domain": "CONTOSO",
    "Password": "WebAppP@ssw0rd"
  },
  "Delegation": {
    "TargetUsername": "john.doe@contoso.com",
    "TargetServicePrincipalName": "cifs/fileserver.contoso.com",
    "ExternalExecutablePath": "C:\\WebApp\\Bin\\FileShareWriter.exe",
    "FileSharePath": "\\\\fileserver.contoso.com\\UserDocuments\\john_doe\\document.pdf",
    "TimeoutMs": 60000
  }
}
```

**Active Directory Setup**:
```powershell
# Create service account
New-ADUser -Name "svc_webapp" -SamAccountName "svc_webapp" -UserPrincipalName "svc_webapp@contoso.com" -AccountPassword (ConvertTo-SecureString "WebAppP@ssw0rd" -AsPlainText -Force) -Enabled $true

# Configure delegation
Set-ADUser -Identity svc_webapp -Add @{'msDS-AllowedToDelegateTo' = @('cifs/fileserver.contoso.com', 'cifs/fileserver')}
Set-ADAccountControl -Identity svc_webapp -TrustedToAuthForDelegation $true

# Register SPNs
setspn -A cifs/fileserver.contoso.com fileserver
setspn -A cifs/fileserver fileserver
```

### Scenario 2: Multiple File Shares

**Use Case**: Application needs to access different file shares for different users.

**Configuration for User 1**:
```json
{
  "ServiceAccount": {
    "Username": "svc_fileaccess",
    "Domain": "CONTOSO",
    "Password": "FileAccessP@ssw0rd"
  },
  "Delegation": {
    "TargetUsername": "alice@contoso.com",
    "TargetServicePrincipalName": "cifs/fileserver1.contoso.com",
    "ExternalExecutablePath": "C:\\Apps\\FileShareWriter.exe",
    "FileSharePath": "\\\\fileserver1.contoso.com\\AliceShare\\data.txt"
  }
}
```

**Configuration for User 2**:
```json
{
  "ServiceAccount": {
    "Username": "svc_fileaccess",
    "Domain": "CONTOSO",
    "Password": "FileAccessP@ssw0rd"
  },
  "Delegation": {
    "TargetUsername": "bob@contoso.com",
    "TargetServicePrincipalName": "cifs/fileserver2.contoso.com",
    "ExternalExecutablePath": "C:\\Apps\\FileShareWriter.exe",
    "FileSharePath": "\\\\fileserver2.contoso.com\\BobShare\\data.txt"
  }
}
```

**Active Directory Setup**:
```powershell
# Configure delegation for multiple file servers
Set-ADUser -Identity svc_fileaccess -Add @{
    'msDS-AllowedToDelegateTo' = @(
        'cifs/fileserver1.contoso.com',
        'cifs/fileserver1',
        'cifs/fileserver2.contoso.com',
        'cifs/fileserver2'
    )
}
Set-ADAccountControl -Identity svc_fileaccess -TrustedToAuthForDelegation $true
```

### Scenario 3: DFS Namespace Access

**Use Case**: Access files through DFS namespace with user credentials.

**Configuration**:
```json
{
  "ServiceAccount": {
    "Username": "svc_dfs",
    "Domain": "CONTOSO",
    "Password": "DfsP@ssw0rd"
  },
  "Delegation": {
    "TargetUsername": "user@contoso.com",
    "TargetServicePrincipalName": "cifs/dfs.contoso.com",
    "ExternalExecutablePath": "C:\\Apps\\FileShareWriter.exe",
    "FileSharePath": "\\\\contoso.com\\DfsRoot\\Department\\file.txt"
  }
}
```

**Active Directory Setup**:
```powershell
# Register DFS namespace SPN
setspn -A cifs/dfs.contoso.com dfs-server

# Configure delegation
Set-ADUser -Identity svc_dfs -Add @{'msDS-AllowedToDelegateTo' = @('cifs/dfs.contoso.com')}
Set-ADAccountControl -Identity svc_dfs -TrustedToAuthForDelegation $true
```

## Database Access Scenarios

### Scenario 4: SQL Server Access

**Use Case**: Middle-tier service needs to query SQL Server using user credentials.

**Configuration**:
```json
{
  "ServiceAccount": {
    "Username": "svc_middleware",
    "Domain": "CONTOSO",
    "Password": "MiddlewareP@ssw0rd"
  },
  "Delegation": {
    "TargetUsername": "dbuser@contoso.com",
    "TargetServicePrincipalName": "MSSQLSvc/sqlserver.contoso.com:1433",
    "ExternalExecutablePath": "C:\\Apps\\DatabaseQuery.exe",
    "FileSharePath": "\\\\sqlserver.contoso.com\\Results\\query_output.txt",
    "TimeoutMs": 120000
  }
}
```

**Active Directory Setup**:
```powershell
# Register SQL Server SPN (usually done automatically by SQL Server)
setspn -A MSSQLSvc/sqlserver.contoso.com:1433 sqlserver
setspn -A MSSQLSvc/sqlserver.contoso.com sqlserver

# Configure delegation
Set-ADUser -Identity svc_middleware -Add @{
    'msDS-AllowedToDelegateTo' = @(
        'MSSQLSvc/sqlserver.contoso.com:1433',
        'MSSQLSvc/sqlserver.contoso.com'
    )
}
Set-ADAccountControl -Identity svc_middleware -TrustedToAuthForDelegation $true
```

### Scenario 5: Multiple Database Servers

**Use Case**: Application needs to access multiple SQL Server instances.

**Configuration**:
```json
{
  "ServiceAccount": {
    "Username": "svc_dataaccess",
    "Domain": "CONTOSO",
    "Password": "DataAccessP@ssw0rd"
  },
  "Delegation": {
    "TargetUsername": "analyst@contoso.com",
    "TargetServicePrincipalName": "MSSQLSvc/sqlserver1.contoso.com:1433",
    "ExternalExecutablePath": "C:\\Apps\\MultiDbQuery.exe",
    "FileSharePath": "\\\\fileserver.contoso.com\\Reports\\analysis.txt",
    "TimeoutMs": 180000
  }
}
```

**Active Directory Setup**:
```powershell
# Configure delegation for multiple SQL Servers
Set-ADUser -Identity svc_dataaccess -Add @{
    'msDS-AllowedToDelegateTo' = @(
        'MSSQLSvc/sqlserver1.contoso.com:1433',
        'MSSQLSvc/sqlserver2.contoso.com:1433',
        'MSSQLSvc/sqlserver3.contoso.com:1433',
        'cifs/fileserver.contoso.com'  # For writing results
    )
}
Set-ADAccountControl -Identity svc_dataaccess -TrustedToAuthForDelegation $true
```

## Web Application Scenarios

### Scenario 6: ASP.NET Web Application

**Use Case**: ASP.NET application running as service account needs to access backend resources with user identity.

**Configuration**:
```json
{
  "ServiceAccount": {
    "Username": "svc_iis_apppool",
    "Domain": "CONTOSO",
    "Password": "IisAppPoolP@ssw0rd"
  },
  "Delegation": {
    "TargetUsername": "webuser@contoso.com",
    "TargetServicePrincipalName": "cifs/fileserver.contoso.com",
    "ExternalExecutablePath": "C:\\inetpub\\wwwroot\\MyApp\\Bin\\FileShareWriter.exe",
    "FileSharePath": "\\\\fileserver.contoso.com\\WebUploads\\user_file.dat",
    "TimeoutMs": 45000
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

**IIS Application Pool Configuration**:
```powershell
# Create application pool with service account identity
Import-Module WebAdministration
New-WebAppPool -Name "MyAppPool"
Set-ItemProperty IIS:\AppPools\MyAppPool -Name processModel.identityType -Value 3
Set-ItemProperty IIS:\AppPools\MyAppPool -Name processModel.userName -Value "CONTOSO\svc_iis_apppool"
Set-ItemProperty IIS:\AppPools\MyAppPool -Name processModel.password -Value "IisAppPoolP@ssw0rd"
```

### Scenario 7: SharePoint Integration

**Use Case**: SharePoint service needs to access external file shares with user credentials.

**Configuration**:
```json
{
  "ServiceAccount": {
    "Username": "svc_sharepoint",
    "Domain": "CONTOSO",
    "Password": "SharePointP@ssw0rd"
  },
  "Delegation": {
    "TargetUsername": "spuser@contoso.com",
    "TargetServicePrincipalName": "cifs/externalshare.contoso.com",
    "ExternalExecutablePath": "C:\\Program Files\\SharePoint\\Bin\\FileShareWriter.exe",
    "FileSharePath": "\\\\externalshare.contoso.com\\Documents\\imported_file.docx",
    "TimeoutMs": 90000
  }
}
```

## Command-Line Usage Examples

### Example 1: Basic Command-Line Execution

```powershell
dotnet KerberosConstrainedDelegation.dll `
  --service-username svc_delegation `
  --service-domain CONTOSO `
  --service-password "P@ssw0rd123" `
  --target-user testuser@contoso.com `
  --target-spn cifs/fileserver.contoso.com `
  --executable C:\Apps\FileShareWriter.exe `
  --file-share \\fileserver.contoso.com\share\test.txt
```

### Example 2: With Timeout Override

```powershell
dotnet KerberosConstrainedDelegation.dll `
  --service-username svc_delegation `
  --service-domain CONTOSO `
  --service-password "P@ssw0rd123" `
  --target-user testuser@contoso.com `
  --target-spn cifs/fileserver.contoso.com `
  --executable C:\Apps\FileShareWriter.exe `
  --file-share \\fileserver.contoso.com\share\test.txt `
  --timeout 60000
```

### Example 3: Using DOMAIN\username Format

```powershell
dotnet KerberosConstrainedDelegation.dll `
  --service-username svc_delegation `
  --service-domain CONTOSO `
  --service-password "P@ssw0rd123" `
  --target-user "CONTOSO\testuser" `
  --target-spn cifs/fileserver.contoso.com `
  --executable C:\Apps\FileShareWriter.exe `
  --file-share \\fileserver.contoso.com\share\test.txt
```

### Example 4: SQL Server Access

```powershell
dotnet KerberosConstrainedDelegation.dll `
  --service-username svc_sql `
  --service-domain CONTOSO `
  --service-password "SqlP@ssw0rd" `
  --target-user dbadmin@contoso.com `
  --target-spn MSSQLSvc/sqlserver.contoso.com:1433 `
  --executable C:\Apps\SqlQuery.exe `
  --file-share \\fileserver.contoso.com\results\output.txt `
  --timeout 120000
```

### Example 5: Batch Processing Multiple Users

```powershell
# Process delegation for multiple users
$users = @("user1@contoso.com", "user2@contoso.com", "user3@contoso.com")

foreach ($user in $users) {
    Write-Host "Processing delegation for $user..."
    
    dotnet KerberosConstrainedDelegation.dll `
      --service-username svc_batch `
      --service-domain CONTOSO `
      --service-password "BatchP@ssw0rd" `
      --target-user $user `
      --target-spn cifs/fileserver.contoso.com `
      --executable C:\Apps\FileShareWriter.exe `
      --file-share "\\fileserver.contoso.com\users\$($user.Split('@')[0])\data.txt"
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Success for $user" -ForegroundColor Green
    } else {
        Write-Host "Failed for $user (Exit code: $LASTEXITCODE)" -ForegroundColor Red
    }
}
```

## Advanced Configuration

### Scenario 8: Using Environment Variables

**Configuration with Environment Variable Placeholders**:
```json
{
  "ServiceAccount": {
    "Username": "${SERVICE_USERNAME}",
    "Domain": "${SERVICE_DOMAIN}",
    "Password": "${SERVICE_PASSWORD}"
  },
  "Delegation": {
    "TargetUsername": "${TARGET_USER}",
    "TargetServicePrincipalName": "${TARGET_SPN}",
    "ExternalExecutablePath": "${EXECUTABLE_PATH}",
    "FileSharePath": "${FILE_SHARE_PATH}"
  }
}
```

**PowerShell Script to Set Environment Variables**:
```powershell
# Set environment variables
$env:SERVICE_USERNAME = "svc_delegation"
$env:SERVICE_DOMAIN = "CONTOSO"
$env:SERVICE_PASSWORD = "P@ssw0rd123"
$env:TARGET_USER = "testuser@contoso.com"
$env:TARGET_SPN = "cifs/fileserver.contoso.com"
$env:EXECUTABLE_PATH = "C:\Apps\FileShareWriter.exe"
$env:FILE_SHARE_PATH = "\\fileserver.contoso.com\share\test.txt"

# Run application
dotnet KerberosConstrainedDelegation.dll
```

### Scenario 9: Configuration per Environment

**Development Environment (appsettings.Development.json)**:
```json
{
  "ServiceAccount": {
    "Username": "svc_dev",
    "Domain": "DEV",
    "Password": "DevP@ssw0rd"
  },
  "Delegation": {
    "TargetUsername": "devuser@dev.contoso.com",
    "TargetServicePrincipalName": "cifs/devfileserver.dev.contoso.com",
    "ExternalExecutablePath": "C:\\Dev\\FileShareWriter.exe",
    "FileSharePath": "\\\\devfileserver.dev.contoso.com\\share\\test.txt",
    "TimeoutMs": 30000
  },
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  }
}
```

**Production Environment (appsettings.Production.json)**:
```json
{
  "ServiceAccount": {
    "Username": "svc_prod",
    "Domain": "CONTOSO",
    "Password": "ProdP@ssw0rd"
  },
  "Delegation": {
    "TargetUsername": "produser@contoso.com",
    "TargetServicePrincipalName": "cifs/fileserver.contoso.com",
    "ExternalExecutablePath": "C:\\Apps\\Production\\FileShareWriter.exe",
    "FileSharePath": "\\\\fileserver.contoso.com\\production\\data.txt",
    "TimeoutMs": 60000
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  }
}
```

**Run with Specific Environment**:
```powershell
# Development
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet KerberosConstrainedDelegation.dll

# Production
$env:ASPNETCORE_ENVIRONMENT = "Production"
dotnet KerberosConstrainedDelegation.dll
```

## Production Configuration

### Scenario 10: Production with Azure Key Vault

**Configuration (appsettings.Production.json)**:
```json
{
  "ServiceAccount": {
    "Username": "svc_prod",
    "Domain": "CONTOSO",
    "Password": "#{KeyVault:ServiceAccountPassword}#"
  },
  "Delegation": {
    "TargetUsername": "produser@contoso.com",
    "TargetServicePrincipalName": "cifs/fileserver.contoso.com",
    "ExternalExecutablePath": "C:\\Apps\\Production\\FileShareWriter.exe",
    "FileSharePath": "\\\\fileserver.contoso.com\\production\\data.txt",
    "TimeoutMs": 60000
  },
  "AzureKeyVault": {
    "VaultUri": "https://mykeyvault.vault.azure.net/",
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

**Note**: Requires custom implementation to integrate Azure Key Vault.

### Scenario 11: Production with Windows Credential Manager

**PowerShell Script to Store Credentials**:
```powershell
# Store service account password in Windows Credential Manager
cmdkey /add:KerberosDelegation_ServiceAccount /user:CONTOSO\svc_prod /pass:ProdP@ssw0rd

# Verify credential is stored
cmdkey /list | Select-String "KerberosDelegation"
```

**Configuration (appsettings.Production.json)**:
```json
{
  "ServiceAccount": {
    "Username": "svc_prod",
    "Domain": "CONTOSO",
    "Password": "#{CredentialManager:KerberosDelegation_ServiceAccount}#"
  },
  "Delegation": {
    "TargetUsername": "produser@contoso.com",
    "TargetServicePrincipalName": "cifs/fileserver.contoso.com",
    "ExternalExecutablePath": "C:\\Apps\\Production\\FileShareWriter.exe",
    "FileSharePath": "\\\\fileserver.contoso.com\\production\\data.txt"
  }
}
```

**Note**: Requires custom implementation to read from Windows Credential Manager.

### Scenario 12: High-Security Production Configuration

**Configuration with Minimal Logging**:
```json
{
  "ServiceAccount": {
    "Username": "svc_secure",
    "Domain": "CONTOSO",
    "Password": "SecureP@ssw0rd123!"
  },
  "Delegation": {
    "TargetUsername": "secureuser@contoso.com",
    "TargetServicePrincipalName": "cifs/securefileserver.contoso.com",
    "ExternalExecutablePath": "C:\\Apps\\Secure\\FileShareWriter.exe",
    "FileSharePath": "\\\\securefileserver.contoso.com\\secure\\classified.txt",
    "TimeoutMs": 30000
  },
  "Logging": {
    "LogLevel": {
      "Default": "Error"
    }
  },
  "Security": {
    "AuditAllOperations": true,
    "RequireSignedExecutables": true,
    "MaxConcurrentOperations": 5
  }
}
```

**Active Directory Setup with Enhanced Security**:
```powershell
# Create service account with enhanced security
New-ADUser -Name "svc_secure" `
    -SamAccountName "svc_secure" `
    -UserPrincipalName "svc_secure@contoso.com" `
    -AccountPassword (ConvertTo-SecureString "SecureP@ssw0rd123!" -AsPlainText -Force) `
    -Enabled $true `
    -PasswordNeverExpires $false `
    -ChangePasswordAtLogon $false `
    -KerberosEncryptionType AES256

# Configure constrained delegation (not unconstrained)
Set-ADUser -Identity svc_secure -Add @{
    'msDS-AllowedToDelegateTo' = @('cifs/securefileserver.contoso.com')
}
Set-ADAccountControl -Identity svc_secure -TrustedToAuthForDelegation $true

# Ensure account is not marked for unconstrained delegation
Set-ADAccountControl -Identity svc_secure -TrustedForDelegation $false

# Enable auditing
Set-ADUser -Identity svc_secure -Replace @{
    'msDS-User-Account-Control-Computed' = 0x00080000  # TRUSTED_TO_AUTH_FOR_DELEGATION
}
```

## Configuration Validation Checklist

Before deploying, verify:

- [ ] Service account credentials are correct
- [ ] Service account is enabled and not locked
- [ ] Service account is configured for constrained delegation
- [ ] Target SPN is in the allowed delegation list
- [ ] Protocol transition is enabled (TrustedToAuthForDelegation = True)
- [ ] Target SPNs are registered in Active Directory
- [ ] No duplicate SPNs exist
- [ ] Target user exists and is not marked as sensitive
- [ ] File share path is accessible
- [ ] Executable path is valid
- [ ] Timeout value is appropriate for the operation
- [ ] Logging level is appropriate for the environment
- [ ] Passwords are stored securely (not in plain text in production)

## Testing Configuration

```powershell
# Test configuration script
$config = Get-Content "appsettings.json" | ConvertFrom-Json

Write-Host "=== Configuration Validation ===" -ForegroundColor Cyan

# Test service account
Write-Host "`n[1] Testing service account..." -ForegroundColor Yellow
$serviceAccount = "$($config.ServiceAccount.Domain)\$($config.ServiceAccount.Username)"
try {
    Get-ADUser -Identity $config.ServiceAccount.Username -ErrorAction Stop | Out-Null
    Write-Host "  [OK] Service account exists" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Service account not found" -ForegroundColor Red
}

# Test target user
Write-Host "`n[2] Testing target user..." -ForegroundColor Yellow
$targetUser = $config.Delegation.TargetUsername
try {
    if ($targetUser -match "@") {
        Get-ADUser -Filter {UserPrincipalName -eq $targetUser} -ErrorAction Stop | Out-Null
    } else {
        $username = $targetUser.Split('\')[1]
        Get-ADUser -Identity $username -ErrorAction Stop | Out-Null
    }
    Write-Host "  [OK] Target user exists" -ForegroundColor Green
} catch {
    Write-Host "  [ERROR] Target user not found" -ForegroundColor Red
}

# Test SPN registration
Write-Host "`n[3] Testing SPN registration..." -ForegroundColor Yellow
$spn = $config.Delegation.TargetServicePrincipalName
$spnCheck = setspn -Q $spn 2>&1
if ($spnCheck -match "Existing SPN found") {
    Write-Host "  [OK] SPN is registered" -ForegroundColor Green
} else {
    Write-Host "  [ERROR] SPN not found" -ForegroundColor Red
}

# Test executable path
Write-Host "`n[4] Testing executable path..." -ForegroundColor Yellow
if (Test-Path $config.Delegation.ExternalExecutablePath) {
    Write-Host "  [OK] Executable exists" -ForegroundColor Green
} else {
    Write-Host "  [ERROR] Executable not found" -ForegroundColor Red
}

# Test file share path
Write-Host "`n[5] Testing file share accessibility..." -ForegroundColor Yellow
$sharePath = Split-Path $config.Delegation.FileSharePath -Parent
if (Test-Path $sharePath) {
    Write-Host "  [OK] File share is accessible" -ForegroundColor Green
} else {
    Write-Host "  [WARNING] File share may not be accessible" -ForegroundColor Yellow
}

Write-Host "`n=== Validation Complete ===" -ForegroundColor Cyan
```

## Additional Resources

- [SETUP_GUIDE.md](SETUP_GUIDE.md) - Detailed setup instructions
- [TROUBLESHOOTING.md](TROUBLESHOOTING.md) - Common issues and solutions
- [README.md](../README.md) - Project overview and quick start
