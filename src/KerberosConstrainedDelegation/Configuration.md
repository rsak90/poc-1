# Configuration Guide

## Overview

The Kerberos Constrained Delegation application supports configuration through:
1. **appsettings.json** file (recommended for development)
2. **Command-line arguments** (recommended for production/automation)

## Configuration Settings

### Service Account Settings

The service account is used to authenticate and perform delegation operations.

- **ServiceAccount:Username** - Username of the service account (without domain)
  - Example: `svc_delegation`
  - Required: Yes
  - Validation: Must not contain invalid characters (\ / : * ? " < > |)

- **ServiceAccount:Domain** - Domain name for the service account
  - Example: `CONTOSO`
  - Required: Yes

- **ServiceAccount:Password** - Password for the service account
  - Example: `YourServiceAccountPassword`
  - Required: Yes
  - Security: Stored as SecureString in memory, never logged

### Target User Settings

The target user is the user whose identity will be impersonated.

- **TargetUser:Username** - Username to impersonate
  - Format: UPN (`user@domain.com`) or DOMAIN\username (`CONTOSO\user`)
  - Example: `user@contoso.com` or `CONTOSO\user`
  - Required: Yes
  - Validation: Must be valid UPN or DOMAIN\username format

### Target Service Settings

The target service is the service that will be accessed with delegated credentials.

- **TargetService:Spn** - Service Principal Name of the target service
  - Format: `service/host.domain.com` or `service/host.domain.com:port`
  - Example: `cifs/fileserver.contoso.com` or `http/webapp.contoso.com:8080`
  - Required: Yes
  - Validation: Must match SPN format pattern

### External Executable Settings

The external executable is spawned with delegated credentials to demonstrate double-hop authentication.

- **ExternalExecutable:Path** - Absolute path to the executable
  - Example: `C:\Tools\FileShareWriter.exe`
  - Required: Yes
  - Validation: File must exist at the specified path

### File Share Settings

The file share is used to test double-hop authentication.

- **FileShare:Path** - UNC path to the file share
  - Format: `\\server\share\path\file.txt`
  - Example: `\\fileserver\share\test.txt`
  - Required: Yes
  - Validation: Must be valid UNC path format

### Execution Settings

- **Execution:TimeoutSeconds** - Maximum time to wait for process completion
  - Example: `30`
  - Required: No (default: 30 seconds)
  - Validation: Must be a positive integer

## Configuration via appsettings.json

Create or edit `appsettings.json` in the application directory:

```json
{
  "ServiceAccount": {
    "Username": "svc_delegation",
    "Domain": "CONTOSO",
    "Password": "YourServiceAccountPassword"
  },
  "TargetUser": {
    "Username": "user@contoso.com"
  },
  "TargetService": {
    "Spn": "cifs/fileserver.contoso.com"
  },
  "ExternalExecutable": {
    "Path": "C:\\Path\\To\\FileShareWriter.exe"
  },
  "FileShare": {
    "Path": "\\\\fileserver\\share\\test.txt"
  },
  "Execution": {
    "TimeoutSeconds": 30
  }
}
```

**Security Note**: The appsettings.json file contains sensitive credentials. Ensure proper file permissions and consider using encrypted configuration or credential managers in production.

## Configuration via Command-Line Arguments

Override configuration settings using command-line arguments:

```bash
KerberosConstrainedDelegation.exe ^
  --ServiceAccount:Username=svc_delegation ^
  --ServiceAccount:Domain=CONTOSO ^
  --ServiceAccount:Password=YourPassword ^
  --TargetUser:Username=user@contoso.com ^
  --TargetService:Spn=cifs/fileserver.contoso.com ^
  --ExternalExecutable:Path=C:\Tools\FileShareWriter.exe ^
  --FileShare:Path=\\fileserver\share\test.txt ^
  --Execution:TimeoutSeconds=30
```

Command-line arguments take precedence over appsettings.json values.

## Validation

The application validates all configuration settings before execution:

1. **Service Account Credentials**
   - Username, domain, and password must be provided
   - Username must not contain invalid characters

2. **Target Username**
   - Must be in valid UPN or DOMAIN\username format
   - Examples: `user@contoso.com`, `CONTOSO\user`

3. **Target SPN**
   - Must match pattern: `service/host.domain.com`
   - Examples: `cifs/fileserver.contoso.com`, `http/webapp.contoso.com:8080`

4. **Executable Path**
   - Must be an absolute path
   - File must exist at the specified location

5. **File Share Path**
   - Must be a valid UNC path
   - Must start with `\\`
   - Must have at least server and share components

6. **Timeout**
   - Must be a positive integer (if specified)

If validation fails, the application will display a detailed error message and exit with code 1.

## Security Best Practices

1. **Credential Storage**
   - Never commit appsettings.json with real credentials to source control
   - Use `.gitignore` to exclude configuration files
   - Consider using Windows Credential Manager or Azure Key Vault for production

2. **File Permissions**
   - Restrict read access to appsettings.json to authorized users only
   - Use NTFS permissions to protect the configuration file

3. **Password Handling**
   - Passwords are converted to SecureString in memory
   - Passwords are never logged or displayed
   - SecureString is properly disposed after use

4. **Command-Line Arguments**
   - Be aware that command-line arguments may be visible in process listings
   - Prefer configuration files over command-line for sensitive data
   - Use encrypted configuration in production environments

## Troubleshooting

### Configuration Validation Failed

If you see "Configuration validation failed", check the error message for details:

- **Missing configuration key**: Ensure all required settings are present
- **Invalid format**: Check that usernames, SPNs, and paths match the expected format
- **File not found**: Verify the executable path points to an existing file
- **Invalid characters**: Remove special characters from usernames

### Configuration Key Not Found

If you see "Configuration key 'X' is missing or empty":

- Verify the key exists in appsettings.json
- Check for typos in key names (case-sensitive)
- Ensure the JSON file is valid (use a JSON validator)
- Verify appsettings.json is copied to the output directory

### Invalid Username Format

Username must be in one of these formats:
- UPN: `user@domain.com`
- DOMAIN\username: `CONTOSO\user`

### Invalid SPN Format

SPN must follow the pattern:
- `service/host.domain.com`
- `service/host.domain.com:port`

Examples:
- `cifs/fileserver.contoso.com`
- `http/webapp.contoso.com:8080`
- `mssql/sqlserver.contoso.com:1433`

### Invalid UNC Path

UNC path must:
- Start with `\\`
- Include server name and share name
- Example: `\\fileserver\share\folder\file.txt`
