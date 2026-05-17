# LSA Connection Fix - STATUS_PRIVILEGE_NOT_HELD Error

## Problem
The application was failing with error code `-10737441759` (hex: `0xC0000061`, `STATUS_PRIVILEGE_NOT_HELD`) when calling `LsaRegisterLogonProcess`.

## Root Cause
`LsaRegisterLogonProcess` requires the `SeTcbPrivilege` (Act as part of the operating system) privilege, which is typically only available to:
- Processes running as SYSTEM
- Processes with elevated administrator privileges and explicit privilege assignment

## Solution
Updated the `RegisterLsaAuthenticationPackage` method to use `LsaConnectUntrusted` as the primary connection method, with `LsaRegisterLogonProcess` as a fallback.

### Changes Made

#### 1. Added `LsaConnectUntrusted` P/Invoke Declaration
**File:** `NativeMethods.cs`

```csharp
/// <summary>
/// Establishes an untrusted connection to the LSA (does not require special privileges)
/// </summary>
[DllImport("secur32.dll", SetLastError = true)]
public static extern int LsaConnectUntrusted(out IntPtr LsaHandle);
```

#### 2. Added `STATUS_PRIVILEGE_NOT_HELD` Constant
**File:** `NativeMethods.cs`

```csharp
public const int STATUS_PRIVILEGE_NOT_HELD = unchecked((int)0xC0000061);
```

#### 3. Updated `RegisterLsaAuthenticationPackage` Method
**File:** `KerberosTokenManager.cs`

The method now:
1. First attempts to connect using `LsaConnectUntrusted` (no special privileges required)
2. Falls back to `LsaRegisterLogonProcess` if the untrusted connection fails
3. Provides a clear error message if both methods fail

### Key Differences Between the Two APIs

| Feature | LsaConnectUntrusted | LsaRegisterLogonProcess |
|---------|---------------------|-------------------------|
| **Privileges Required** | None | SeTcbPrivilege |
| **Connection Type** | Untrusted | Trusted |
| **Use Case** | Standard applications | System services, privileged operations |
| **Elevation Required** | No | Yes (typically) |

### Benefits
- **Works without elevation**: The application can now run without administrator privileges in most scenarios
- **Graceful fallback**: If elevated privileges are available, the trusted connection is still attempted
- **Better error messages**: Clear indication when privilege issues occur

### Testing
Build the project and run it without administrator privileges. The `LsaConnectUntrusted` path should succeed for standard Kerberos delegation operations.

If you need the trusted connection for specific scenarios, run the application as administrator or ensure the service account has `SeTcbPrivilege`.

## Additional Notes
- Both connection methods work with `LsaLookupAuthenticationPackage` and `LsaLogonUser`
- For most Kerberos constrained delegation scenarios, the untrusted connection is sufficient
- The untrusted connection still allows S4U2Self and S4U2Proxy operations when the service account is properly configured in Active Directory
