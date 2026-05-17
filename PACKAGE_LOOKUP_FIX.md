# Authentication Package Lookup Fix

## Problem
`LsaLookupAuthenticationPackage` was returning an invalid package ID (0) when looking up "Negotiate", causing S4U2Self and S4U2Proxy operations to fail.

## Root Cause
The authentication package name "Negotiate" may not be recognized by the LSA on all systems, or the package may be registered under a different name. Different Windows versions and configurations may use different package names.

## Solution
Updated the code to try multiple authentication package names in order of preference until a valid one is found.

### Changes Made

#### 1. Added Authentication Package Name Constants
**File:** `NativeMethods.cs`

```csharp
// Authentication Package Names
public const string MSV1_0_PACKAGE_NAME = "MICROSOFT_AUTHENTICATION_PACKAGE_V1_0";
public const string NEGOSSP_NAME = "Negotiate";
public const string MICROSOFT_KERBEROS_NAME = "Kerberos";
```

#### 2. Updated `RegisterLsaAuthenticationPackage` to Try Multiple Names
**File:** `KerberosTokenManager.cs`

The method now:
1. Tries multiple package names in order:
   - "Negotiate" (preferred for S4U operations)
   - "Kerberos" (direct Kerberos package)
   - "MICROSOFT_AUTHENTICATION_PACKAGE_V1_0" (MSV1_0, fallback)
2. Returns the first valid package ID found
3. Provides detailed error messages showing all attempted names

```csharp
// Try multiple package names in order of preference
string[] packageNamesToTry = packageName == "Negotiate" 
    ? new[] { NativeMethods.NEGOSSP_NAME, NativeMethods.MICROSOFT_KERBEROS_NAME, NativeMethods.MSV1_0_PACKAGE_NAME }
    : new[] { packageName, NativeMethods.MSV1_0_PACKAGE_NAME };

foreach (var pkgName in packageNamesToTry)
{
    // Try to lookup this package name
    status = NativeMethods.LsaLookupAuthenticationPackage(
        _lsaHandle,
        ref authPackageName,
        out _authenticationPackage);

    if (status == NativeMethods.STATUS_SUCCESS && _authenticationPackage != 0)
    {
        // Success! Return the valid package ID
        return _authenticationPackage;
    }
}
```

## Authentication Package Names Explained

### 1. "Negotiate" (NEGOSSP_NAME)
- **Best for**: S4U2Self and S4U2Proxy operations
- **Description**: The Negotiate Security Support Provider automatically selects between Kerberos and NTLM
- **Availability**: Windows 2000 and later
- **Use case**: Preferred for constrained delegation

### 2. "Kerberos" (MICROSOFT_KERBEROS_NAME)
- **Best for**: Direct Kerberos authentication
- **Description**: The Kerberos v5 authentication protocol
- **Availability**: Domain-joined Windows systems
- **Use case**: When you specifically need Kerberos (not NTLM fallback)

### 3. "MICROSOFT_AUTHENTICATION_PACKAGE_V1_0" (MSV1_0_PACKAGE_NAME)
- **Best for**: Fallback authentication
- **Description**: The MSV1_0 authentication package (NTLM)
- **Availability**: All Windows systems
- **Use case**: Last resort fallback, supports S4U operations

## Why Package Lookup Can Fail

1. **System Configuration**
   - Package not installed or disabled
   - Group Policy restrictions
   - Security software interference

2. **Domain vs Workgroup**
   - Kerberos requires domain membership
   - Workgroup systems may only have MSV1_0

3. **Windows Version**
   - Older systems may not support Negotiate
   - Different package names on different versions

4. **LSA Connection Type**
   - `LsaConnectUntrusted` may have limited package access
   - `LsaRegisterLogonProcess` (with SeTcbPrivilege) has full access

## Troubleshooting

### If All Package Lookups Fail

1. **Check if you're on a domain-joined system**
   ```powershell
   (Get-WmiObject -Class Win32_ComputerSystem).PartOfDomain
   ```

2. **Verify Kerberos is available**
   ```powershell
   klist
   ```

3. **Check LSA connection**
   - Try running as Administrator
   - Check if security software is blocking LSA access

4. **List available authentication packages**
   Use a tool like Process Monitor to see what packages are registered

### Error Messages

The new implementation provides detailed error messages:

```
Failed to lookup any authentication package. 
Tried: Negotiate, Kerberos, MICROSOFT_AUTHENTICATION_PACKAGE_V1_0. 
Last error: LsaLookupAuthenticationPackage returned invalid package ID (0) for 'MICROSOFT_AUTHENTICATION_PACKAGE_V1_0'.
```

This tells you:
- Which packages were attempted
- Which one failed last
- Why it failed

## Testing

Build and run the application. The code will automatically try multiple package names:

```bash
dotnet build
dotnet run
```

The application should now work on more systems by automatically finding an available authentication package.

## Additional Notes

- The package lookup is cached after the first successful lookup
- The order of package names matters (Negotiate is tried first for best compatibility)
- If you need a specific package, you can still pass it explicitly to `RegisterLsaAuthenticationPackage()`
- The MSV1_0 package should be available on all Windows systems as a last resort
