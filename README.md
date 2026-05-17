# Kerberos Constrained Delegation Application

A C# console application that implements Kerberos constrained delegation using S4U2Self and S4U2Proxy extensions to solve the double-hop authentication problem in Windows environments.

## Table of Contents

- [Overview](#overview)
- [Purpose](#purpose)
- [Use Cases](#use-cases)
- [Prerequisites](#prerequisites)
- [Project Structure](#project-structure)
- [Building the Solution](#building-the-solution)
- [Configuration](#configuration)
- [Usage Examples](#usage-examples)
- [Troubleshooting](#troubleshooting)
- [Architecture](#architecture)
- [Security Considerations](#security-considerations)
- [License](#license)

## Overview

This application demonstrates how to implement Kerberos constrained delegation in C# to solve the "double-hop" authentication problem. The double-hop problem occurs when a middle-tier service needs to access backend resources (like file shares or databases) using the original user's identity rather than the service's own identity.

The application uses Windows Security APIs to:
1. Authenticate as a service account
2. Obtain a Kerberos ticket for a target user (S4U2Self)
3. Obtain a delegated service ticket for a backend resource (S4U2Proxy)
4. Spawn a process with the delegated credentials
5. Access a network file share to demonstrate successful delegation

## Purpose

The primary purposes of this application are:

- **Demonstrate Kerberos Constrained Delegation**: Show how to implement S4U2Self and S4U2Proxy in C#
- **Solve Double-Hop Authentication**: Enable middle-tier services to access backend resources with user credentials
- **Educational Reference**: Provide a working example of Windows Security API usage for delegation
- **Testing and Validation**: Verify Active Directory delegation configuration is working correctly

## Use Cases

This application is useful for:

1. **Web Applications**: A web server needs to access a file share or database using the authenticated user's credentials
2. **Service-Oriented Architecture**: A middle-tier service needs to call backend services with user identity
3. **Scheduled Tasks**: A service account needs to perform operations on behalf of different users
4. **Auditing and Compliance**: Maintain user identity throughout the authentication chain for audit trails
5. **Testing Delegation Setup**: Validate that Active Directory constrained delegation is configured correctly

## Prerequisites

### Operating System
- Windows Server 2008 R2 or higher
- Windows 7 or higher
- Active Directory domain membership required

### Software Requirements
- .NET 9.0 SDK or higher
- Visual Studio 2022 or Visual Studio Code (for development)
- PowerShell 5.1 or higher (for setup scripts)

### Active Directory Requirements
- Active Directory Domain Services (AD DS)
- Domain functional level: Windows Server 2008 or higher
- Service account with constrained delegation configured
- Target Service Principal Names (SPNs) registered
- Network file share for testing

### Permissions
- Service account must be trusted for delegation to specific SPNs
- Service account must have "Act as part of the operating system" privilege (for production)
- User running the application must have appropriate permissions to authenticate as the service account

## Project Structure

```
offline-jobs/
├── src/
│   ├── KerberosConstrainedDelegation/    # Main console application
│   │   ├── Program.cs                     # Application entry point
│   │   ├── KerberosTokenManager.cs        # Token management and delegation
│   │   ├── ProcessSpawner.cs              # Process spawning with delegated credentials
│   │   ├── ConfigurationManager.cs        # Configuration loading and validation
│   │   ├── NativeMethods.cs               # Windows API P/Invoke declarations
│   │   ├── ServiceAccountCredentials.cs   # Service account credential model
│   │   ├── ProcessExecutionResult.cs      # Process execution result model
│   │   ├── UserIdentityInfo.cs            # User identity information model
│   │   ├── KerberosException.cs           # Custom exception for Kerberos errors
│   │   ├── KerberosErrorType.cs           # Enumeration of error types
│   │   ├── ValidationResult.cs            # Configuration validation result
│   │   ├── appsettings.json               # Application configuration
│   │   └── appsettings.template.json      # Configuration template
│   └── FileShareWriter/                   # External process for testing delegation
│       ├── Program.cs                     # Writes to file share with delegated credentials
│       └── FileShareWriter.csproj
├── tests/
│   └── KerberosConstrainedDelegation.Tests/  # Unit tests
│       └── KerberosConstrainedDelegation.Tests.csproj
├── docs/
│   ├── SETUP_GUIDE.md                     # Detailed setup instructions
│   ├── TROUBLESHOOTING.md                 # Troubleshooting guide
│   └── CONFIGURATION_EXAMPLES.md          # Configuration examples
├── MANUAL_TEST_GUIDE.md                   # Manual testing guide
└── KerberosConstrainedDelegation.sln      # Solution file
```

## Building the Solution

### Build from Command Line

```powershell
# Clone or navigate to the repository
cd offline-jobs

# Restore dependencies
dotnet restore

# Build the solution
dotnet build KerberosConstrainedDelegation.sln

# Build in Release mode
dotnet build KerberosConstrainedDelegation.sln -c Release

# Run tests
dotnet test

# Publish for deployment
dotnet publish src/KerberosConstrainedDelegation/KerberosConstrainedDelegation.csproj -c Release -o ./publish
```

### Build from Visual Studio

1. Open `KerberosConstrainedDelegation.sln` in Visual Studio 2022
2. Select **Build > Build Solution** (or press Ctrl+Shift+B)
3. The output will be in `src/KerberosConstrainedDelegation/bin/Debug/net9.0/`

## Configuration

The application can be configured using `appsettings.json` or command-line arguments.

### Configuration File (appsettings.json)

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
    "ExternalExecutablePath": "C:\\Path\\To\\FileShareWriter.exe",
    "FileSharePath": "\\\\fileserver.contoso.com\\share\\test.txt",
    "TimeoutMs": 30000
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Command-Line Arguments

```powershell
dotnet run --project src/KerberosConstrainedDelegation/KerberosConstrainedDelegation.csproj -- `
  --service-username svc_delegation `
  --service-domain CONTOSO `
  --service-password YourSecurePassword `
  --target-user testuser@contoso.com `
  --target-spn cifs/fileserver.contoso.com `
  --executable C:\Path\To\FileShareWriter.exe `
  --file-share \\fileserver.contoso.com\share\test.txt
```

### Configuration Parameters

| Parameter | Description | Format | Required |
|-----------|-------------|--------|----------|
| ServiceAccount.Username | Service account username | String (no domain) | Yes |
| ServiceAccount.Domain | Service account domain | String | Yes |
| ServiceAccount.Password | Service account password | String (stored as SecureString) | Yes |
| Delegation.TargetUsername | User to impersonate | UPN or DOMAIN\username | Yes |
| Delegation.TargetServicePrincipalName | Target SPN for delegation | service/host.domain.com | Yes |
| Delegation.ExternalExecutablePath | Path to FileShareWriter.exe | Absolute path | Yes |
| Delegation.FileSharePath | UNC path to test file | \\\\server\\share\\file.txt | Yes |
| Delegation.TimeoutMs | Process timeout in milliseconds | Integer (default: 30000) | No |

See [CONFIGURATION_EXAMPLES.md](docs/CONFIGURATION_EXAMPLES.md) for more detailed configuration examples.

## Usage Examples

### Basic Usage

```powershell
# Run with configuration file
cd src/KerberosConstrainedDelegation
dotnet run

# Run with command-line arguments
dotnet run -- --service-username svc_delegation --service-domain CONTOSO --service-password P@ssw0rd --target-user testuser@contoso.com --target-spn cifs/fileserver.contoso.com --executable C:\FileShareWriter.exe --file-share \\fileserver\share\test.txt
```

### Expected Output (Success)

```
[INFO] Loading configuration...
[INFO] Configuration validated successfully
[INFO] Initializing Kerberos Token Manager...
[INFO] Validating delegation configuration...
[INFO] Delegation configuration is valid
[INFO] Obtaining delegated token for user: testuser@contoso.com
[INFO] Target SPN: cifs/fileserver.contoso.com
[INFO] Successfully obtained delegated token
[INFO] Spawning process: C:\FileShareWriter.exe
[INFO] Arguments: "\\fileserver\share\test.txt" "Test content from delegated process"
[INFO] Process exit code: 0
[INFO] Execution time: 1.23s
[INFO] Standard Output:
Running as: CONTOSO\testuser
Authentication type: Kerberos
Is authenticated: True
SID: S-1-5-21-...
Successfully wrote to: \\fileserver\share\test.txt
[INFO] Delegation test completed successfully
```

### Example Scenarios

#### Scenario 1: Web Application File Access
A web application running as `svc_webapp` needs to save user-uploaded files to a file share with the user's credentials:

```json
{
  "ServiceAccount": {
    "Username": "svc_webapp",
    "Domain": "CONTOSO",
    "Password": "WebAppPassword"
  },
  "Delegation": {
    "TargetUsername": "john.doe@contoso.com",
    "TargetServicePrincipalName": "cifs/fileserver.contoso.com",
    "ExternalExecutablePath": "C:\\WebApp\\FileWriter.exe",
    "FileSharePath": "\\\\fileserver.contoso.com\\uploads\\document.pdf"
  }
}
```

#### Scenario 2: Database Access with User Identity
A middle-tier service needs to query a SQL Server database using the original user's credentials:

```json
{
  "ServiceAccount": {
    "Username": "svc_middleware",
    "Domain": "CONTOSO",
    "Password": "MiddlewarePassword"
  },
  "Delegation": {
    "TargetUsername": "jane.smith@contoso.com",
    "TargetServicePrincipalName": "MSSQLSvc/sqlserver.contoso.com:1433",
    "ExternalExecutablePath": "C:\\Middleware\\DatabaseQuery.exe",
    "FileSharePath": "\\\\sqlserver.contoso.com\\data\\query_result.txt"
  }
}
```

## Troubleshooting

### Common Issues

#### Error: "Service authentication failed"
**Cause**: Service account credentials are incorrect or the account is locked/disabled.

**Solution**:
1. Verify service account credentials
2. Check if account is enabled in Active Directory
3. Verify account is not locked out
4. Check password expiration

#### Error: "Service not trusted for delegation"
**Cause**: Service account is not configured for constrained delegation to the target SPN.

**Solution**:
1. Open Active Directory Users and Computers
2. Find the service account
3. Go to the **Delegation** tab
4. Select "Trust this user for delegation to specified services only"
5. Add the target SPN to the list

#### Error: "User not found"
**Cause**: Target username does not exist in Active Directory or format is incorrect.

**Solution**:
1. Verify user exists: `Get-ADUser -Identity username`
2. Use correct format: UPN (`user@domain.com`) or DOMAIN\username
3. Check for typos in username

#### Error: "Access denied" when writing to file share
**Cause**: Target user does not have permissions on the file share.

**Solution**:
1. Verify user has write permissions on the share
2. Check NTFS permissions on the target directory
3. Verify SMB signing requirements are met

For more detailed troubleshooting, see [TROUBLESHOOTING.md](docs/TROUBLESHOOTING.md).

### Diagnostic Commands

```powershell
# Check SPN registration
setspn -L svc_delegation

# View Kerberos tickets
klist

# Test file share access
Test-Path \\fileserver\share

# Check delegation configuration
Get-ADUser svc_delegation -Properties msDS-AllowedToDelegateTo | Select-Object -ExpandProperty msDS-AllowedToDelegateTo
```

## Architecture

### Components

1. **KerberosTokenManager**: Manages Kerberos token lifecycle and delegation operations
2. **ProcessSpawner**: Spawns external processes with delegated credentials
3. **ConfigurationManager**: Loads and validates application configuration
4. **FileShareWriter**: External executable that demonstrates delegation by writing to a file share

### Authentication Flow

```
1. Application authenticates as service account (LogonUser)
2. Execute S4U2Self to obtain user's TGS ticket
3. Execute S4U2Proxy to obtain service ticket for target SPN
4. Spawn FileShareWriter process with delegated token
5. FileShareWriter accesses file share using delegated credentials
6. Verify user identity and write success
```

### Technology Stack

- **Target Framework**: .NET 9.0
- **Language**: C# 12
- **Platform**: Windows (x64/AnyCPU)
- **APIs**: Windows Security APIs (advapi32.dll, kernel32.dll, secur32.dll)

## Security Considerations

### Credential Protection
- Passwords are stored in memory as `SecureString`
- Sensitive data is cleared from memory after use
- Passwords and tokens are never logged

### Token Lifetime
- Delegated tokens are disposed immediately after use
- No token caching (security best practice)
- Constrained delegation only (not unconstrained)

### Least Privilege
- Service account should have minimal permissions
- Delegation limited to specific SPNs only
- Process spawned with minimal privileges

### Audit Logging
- All delegation attempts are logged
- Includes user identity, target SPN, and timestamp
- Win32 error codes logged for failures
- No sensitive data in logs

## License

This project is for demonstration and educational purposes.

## Additional Documentation

- [Setup Guide](docs/SETUP_GUIDE.md) - Detailed Active Directory and application setup
- [Troubleshooting Guide](docs/TROUBLESHOOTING.md) - Common errors and solutions
- [Configuration Examples](docs/CONFIGURATION_EXAMPLES.md) - Example configurations for different scenarios
- [Manual Test Guide](MANUAL_TEST_GUIDE.md) - Step-by-step testing instructions
