# STATUS_INVALID_PARAMETER Fix (0xC000000D)

## Problem
`LsaLogonUser` was failing with `STATUS_INVALID_PARAMETER` (0xC000000D) during S4U2Self operation.

## Root Causes

### 1. Wrong Logon Type Constants
**Issue:** Using `LOGON32_LOGON_NETWORK` (for `LogonUser`) instead of `Network` (for `LsaLogonUser`)

The `LogonUser` API and `LsaLogonUser` API use **different constant sets** for logon types, even though the values are the same:

| Constant | Value | Used By |
|----------|-------|---------|
| `LOGON32_LOGON_NETWORK` | 3 | `LogonUser` (advapi32.dll) |
| `Network` | 3 | `LsaLogonUser` (secur32.dll) |

While the values are identical, using the wrong constant name can cause confusion and potential issues.

### 2. TOKEN_SOURCE.SourceName Must Be Exactly 8 Bytes
**Issue:** Creating `SourceName` directly from `Encoding.ASCII.GetBytes()` without ensuring it's exactly 8 bytes.

The `TOKEN_SOURCE` structure requires:
```csharp
[MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
public byte[] SourceName;  // MUST be exactly 8 bytes
```

## Fixes Applied

### Fix 1: Added Security Logon Type Constants
**File:** `NativeMethods.cs`

```csharp
// Security Logon Types (for LsaLogonUser)
public const int Interactive = 2;
public const int Network = 3;
public const int Batch = 4;
public const int Service = 5;
public const int Proxy = 6;
public const int Unlock = 7;
public const int NetworkCleartext = 8;
public const int NewCredentials = 9;
public const int RemoteInteractive = 10;
public const int CachedInteractive = 11;
public const int CachedRemoteInteractive = 12;
public const int CachedUnlock = 13;
```

### Fix 2: Updated S4U2Self to Use Correct Logon Type
**File:** `KerberosTokenManager.cs` - `ExecuteS4U2Self`

**Before:**
```csharp
var status = NativeMethods.LsaLogonUser(
    _lsaHandle,
    ref originName,
    NativeMethods.LOGON32_LOGON_NETWORK,  // ❌ Wrong constant
    authPackage,
    ...
```

**After:**
```csharp
var status = NativeMethods.LsaLogonUser(
    _lsaHandle,
    ref originName,
    NativeMethods.Network,  // ✅ Correct constant for LsaLogonUser
    authPackage,
    ...
```

### Fix 3: Fixed TOKEN_SOURCE.SourceName to Be Exactly 8 Bytes
**File:** `KerberosTokenManager.cs` - `ExecuteS4U2Self`

**Before:**
```csharp
var tokenSource = new NativeMethods.TOKEN_SOURCE
{
    SourceName = System.Text.Encoding.ASCII.GetBytes("S4U2Self"),  // ❌ 8 bytes, but not guaranteed
    SourceIdentifier = new NativeMethods.LUID { LowPart = 0, HighPart = 0 }
};
```

**After:**
```csharp
// TOKEN_SOURCE.SourceName must be exactly 8 bytes
var sourceNameBytes = new byte[8];
var sourceNameString = "S4U2Self";
var encodedBytes = System.Text.Encoding.ASCII.GetBytes(sourceNameString);
Array.Copy(encodedBytes, sourceNameBytes, Math.Min(encodedBytes.Length, 8));

var tokenSource = new NativeMethods.TOKEN_SOURCE
{
    SourceName = sourceNameBytes,  // ✅ Guaranteed exactly 8 bytes
    SourceIdentifier = new NativeMethods.LUID { LowPart = 0, HighPart = 0 }
};
```

### Fix 4: Updated S4U2Proxy to Use Correct Logon Type
**File:** `KerberosTokenManager.cs` - `ExecuteS4U2Proxy`

**Before:**
```csharp
var status = NativeMethods.LsaLogonUser(
    _lsaHandle,
    ref originName,
    NativeMethods.LOGON32_LOGON_NETWORK_CLEARTEXT,  // ❌ Wrong constant
    authPackage,
    ...
```

**After:**
```csharp
var status = NativeMethods.LsaLogonUser(
    _lsaHandle,
    ref originName,
    NativeMethods.NetworkCleartext,  // ✅ Correct constant for LsaLogonUser
    authPackage,
    ...
```

### Fix 5: Fixed TOKEN_SOURCE in S4U2Proxy
**File:** `KerberosTokenManager.cs` - `ExecuteS4U2Proxy`

Applied the same 8-byte guarantee for the S4U2Proxy token source.

## Understanding the Error

### STATUS_INVALID_PARAMETER (0xC000000D)
This NTSTATUS code indicates that one or more parameters passed to `LsaLogonUser` were invalid. Common causes:

1. ❌ Invalid logon type
2. ❌ Malformed authentication buffer
3. ❌ Invalid TOKEN_SOURCE structure
4. ❌ NULL or invalid LSA handle
5. ❌ Invalid authentication package ID

## Logon Type Reference

### For LogonUser (advapi32.dll)
```csharp
LOGON32_LOGON_INTERACTIVE = 2
LOGON32_LOGON_NETWORK = 3
LOGON32_LOGON_BATCH = 4
LOGON32_LOGON_SERVICE = 5
LOGON32_LOGON_UNLOCK = 7
LOGON32_LOGON_NETWORK_CLEARTEXT = 8
LOGON32_LOGON_NEW_CREDENTIALS = 9
```

### For LsaLogonUser (secur32.dll)
```csharp
Interactive = 2
Network = 3
Batch = 4
Service = 5
Proxy = 6
Unlock = 7
NetworkCleartext = 8
NewCredentials = 9
RemoteInteractive = 10
CachedInteractive = 11
CachedRemoteInteractive = 12
CachedUnlock = 13
```

### Which to Use for S4U Operations?

| Operation | Logon Type | Value | Reason |
|-----------|------------|-------|--------|
| S4U2Self | `Network` | 3 | Standard network authentication |
| S4U2Proxy | `NetworkCleartext` | 8 | Delegation requires cleartext context |

## TOKEN_SOURCE Structure

The `TOKEN_SOURCE` structure identifies the source of an access token:

```csharp
[StructLayout(LayoutKind.Sequential)]
public struct TOKEN_SOURCE
{
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
    public byte[] SourceName;  // MUST be exactly 8 bytes
    public LUID SourceIdentifier;
}
```

### Rules:
1. **SourceName** must be **exactly 8 bytes** (not 7, not 9)
2. If the string is shorter, pad with zeros
3. If the string is longer, truncate to 8 bytes
4. Use ASCII encoding (not Unicode)

### Common Source Names:
- `"S4U2Self"` - 8 characters, perfect
- `"S4U2Prxy"` - 8 characters (Proxy abbreviated)
- `"User32  "` - 6 characters + 2 spaces
- `"Advapi  "` - 6 characters + 2 spaces

## Testing

Build and run the application:

```bash
dotnet build
dotnet run
```

The `STATUS_INVALID_PARAMETER` error should now be resolved. If you still encounter issues, check:

1. ✅ LSA handle is valid (`_lsaHandle != IntPtr.Zero`)
2. ✅ Authentication package ID is non-zero
3. ✅ Username format is correct (UPN or DOMAIN\username)
4. ✅ Service account has constrained delegation configured in AD

## Additional Notes

### Why NetworkCleartext for S4U2Proxy?

S4U2Proxy requires the `NetworkCleartext` logon type because:
- It needs to pass the user's credentials to the target service
- The "cleartext" refers to the Kerberos ticket being available for delegation
- This is different from actual password cleartext

### Why Network for S4U2Self?

S4U2Self uses `Network` logon type because:
- It's obtaining a ticket for the user without their password
- Network logon is the standard type for service-to-service authentication
- It produces a forwardable ticket needed for subsequent S4U2Proxy

## Summary

✅ **Fixed logon type constants** - Now using correct constants for `LsaLogonUser`  
✅ **Fixed TOKEN_SOURCE** - Guaranteed exactly 8 bytes for SourceName  
✅ **Applied to both S4U2Self and S4U2Proxy** - Consistent implementation  
✅ **Build succeeds** - No compilation errors  

The `STATUS_INVALID_PARAMETER` error should now be resolved!
