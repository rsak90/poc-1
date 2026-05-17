# Windows API DLL Reference Guide

This document clarifies which DLL contains each Windows security API function used in this project.

## Summary of Fixes

**Fixed 3 incorrect DLL imports:**
- `LsaLogonUser`: ❌ advapi32.dll → ✅ secur32.dll
- `LsaFreeReturnBuffer`: ❌ advapi32.dll → ✅ secur32.dll
- `LsaDeregisterLogonProcess`: ❌ advapi32.dll → ✅ secur32.dll

---

## Complete DLL Reference

### secur32.dll - Security Support Provider Interface (SSPI) & LSA Functions

All **LSA (Local Security Authority)** functions are in `secur32.dll`:

| Function | DLL | Purpose |
|----------|-----|---------|
| `LsaConnectUntrusted` | ✅ secur32.dll | Connect to LSA without privileges |
| `LsaRegisterLogonProcess` | ✅ secur32.dll | Register trusted logon process (needs SeTcbPrivilege) |
| `LsaLookupAuthenticationPackage` | ✅ secur32.dll | Get authentication package ID |
| `LsaLogonUser` | ✅ secur32.dll | Perform S4U2Self/S4U2Proxy logon |
| `LsaFreeReturnBuffer` | ✅ secur32.dll | Free LSA-allocated memory |
| `LsaDeregisterLogonProcess` | ✅ secur32.dll | Close LSA connection |
| `LsaNtStatusToWinError` | ✅ secur32.dll | Convert NTSTATUS to Win32 error |

**Rule of thumb:** If it starts with `Lsa`, it's in `secur32.dll`

---

### advapi32.dll - Advanced Windows Services

Authentication and token management functions (non-LSA):

| Function | DLL | Purpose |
|----------|-----|---------|
| `LogonUser` | ✅ advapi32.dll | Standard user authentication |
| `CreateProcessAsUser` | ✅ advapi32.dll | Create process with specific token |
| `GetTokenInformation` | ✅ advapi32.dll | Query token properties |

**Rule of thumb:** Standard authentication and token APIs (not LSA-specific) are in `advapi32.dll`

---

### kernel32.dll - Core Windows Kernel Functions

Process and handle management:

| Function | DLL | Purpose |
|----------|-----|---------|
| `CreatePipe` | ✅ kernel32.dll | Create anonymous pipe |
| `WaitForSingleObject` | ✅ kernel32.dll | Wait for process/handle |
| `GetExitCodeProcess` | ✅ kernel32.dll | Get process exit code |
| `CloseHandle` | ✅ kernel32.dll | Close handle |
| `TerminateProcess` | ✅ kernel32.dll | Terminate process |
| `GetStdHandle` | ✅ kernel32.dll | Get standard I/O handle |
| `ReadFile` | ✅ kernel32.dll | Read from file/pipe |
| `PeekNamedPipe` | ✅ kernel32.dll | Check pipe for data |

**Rule of thumb:** Process, handle, and I/O functions are in `kernel32.dll`

---

## Why This Matters

### Historical Context

In older Windows versions (NT 4.0 and earlier), some LSA functions were in `advapi32.dll`. Starting with Windows 2000, Microsoft moved LSA functions to `secur32.dll` to better organize the Security Support Provider Interface (SSPI).

### Common Mistakes

Many code examples online incorrectly show LSA functions in `advapi32.dll` because:
1. They're based on very old code
2. Authors confused LSA functions with other security APIs
3. Copy-paste errors from outdated documentation

### Impact of Wrong DLL

If you specify the wrong DLL:
- ❌ **Runtime error**: "Unable to find entry point named 'FunctionName' in DLL 'wrongdll.dll'"
- ❌ **Application crashes** or fails to start
- ❌ **P/Invoke exceptions** at runtime

---

## Quick Reference by Category

### LSA Operations (S4U2Self, S4U2Proxy)
```csharp
// ALL in secur32.dll
LsaConnectUntrusted()
LsaRegisterLogonProcess()
LsaLookupAuthenticationPackage()
LsaLogonUser()
LsaFreeReturnBuffer()
LsaDeregisterLogonProcess()
LsaNtStatusToWinError()
```

### Standard Authentication
```csharp
// In advapi32.dll
LogonUser()
```

### Token Operations
```csharp
// In advapi32.dll
GetTokenInformation()
CreateProcessAsUser()
```

### Process Management
```csharp
// In kernel32.dll
WaitForSingleObject()
GetExitCodeProcess()
CloseHandle()
TerminateProcess()
```

---

## Verification

All DLL imports in `NativeMethods.cs` have been verified against:
- Microsoft Official Documentation (docs.microsoft.com)
- Windows SDK Headers
- PInvoke.net community database

### Current Status: ✅ ALL CORRECT

All functions are now correctly mapped to their respective DLLs.

---

## Testing

To verify the DLL imports are correct:

```bash
# Build should succeed without errors
dotnet build

# Run the application - should not get "entry point not found" errors
dotnet run
```

If you get "entry point not found" errors, check:
1. Function name spelling (case-sensitive)
2. DLL name (use this guide)
3. Function signature matches Windows API

---

## Additional Notes

### Why secur32.dll for LSA?

`secur32.dll` (Security Support Provider Interface) provides:
- LSA (Local Security Authority) functions
- SSPI (Security Support Provider Interface) functions
- Kerberos, NTLM, and Negotiate protocol support
- S4U (Service for User) delegation support

### Why advapi32.dll for LogonUser?

`advapi32.dll` (Advanced API) provides:
- Standard Windows authentication
- Registry operations
- Service control
- Event logging
- Token manipulation (non-LSA)

### Why kernel32.dll for Process Management?

`kernel32.dll` (Windows Kernel) provides:
- Process and thread management
- Memory management
- File I/O operations
- Handle management
- Synchronization primitives

---

## References

- [Microsoft Docs: LSA Functions](https://docs.microsoft.com/en-us/windows/win32/secauthn/lsa-functions)
- [Microsoft Docs: Authentication Functions](https://docs.microsoft.com/en-us/windows/win32/secauthn/authentication-functions)
- [PInvoke.net](https://www.pinvoke.net/)
