# How to Run with SeTcbPrivilege

## What Changed

The application now **automatically attempts to enable SeTcbPrivilege** when running as Administrator. However, even as Administrator, you need to **grant yourself the privilege** first.

## The Issue

Even when running as Administrator, Windows doesn't automatically give you `SeTcbPrivilege` because it's one of the most powerful privileges. You need to explicitly grant it to your account.

## Solution: Grant SeTcbPrivilege to Your Account

### Step 1: Open Local Security Policy

1. Press `Win + R`
2. Type: `secpol.msc`
3. Press Enter

### Step 2: Navigate to User Rights Assignment

1. Expand: **Local Policies**
2. Click: **User Rights Assignment**

### Step 3: Add Your Account to "Act as part of the operating system"

1. Find: **Act as part of the operating system**
2. Double-click it
3. Click: **Add User or Group**
4. Type your username (e.g., `DOMAIN\YourUsername` or just `YourUsername`)
5. Click: **Check Names** to verify
6. Click: **OK**
7. Click: **OK** again

### Step 4: Restart Your Application

1. Close Visual Studio if it's open
2. Right-click Visual Studio → **Run as Administrator**
3. Open your project
4. Press F5 to debug

The application will now automatically enable `SeTcbPrivilege` and `LsaRegisterLogonProcess` should succeed!

---

## Alternative: Use PsExec to Run as SYSTEM

If you don't want to grant `SeTcbPrivilege` to your account, you can run as SYSTEM using PsExec:

### Download PsExec

1. Download from: https://learn.microsoft.com/en-us/sysinternals/downloads/psexec
2. Extract to a folder (e.g., `C:\Tools\PSTools`)

### Run Your Application as SYSTEM

```cmd
# Open Command Prompt as Administrator
# Navigate to your application directory
cd C:\Users\USER\OneDrive\Desktop\offline-jobs

# Run as SYSTEM
C:\Tools\PSTools\psexec.exe -s -i dotnet run
```

**Explanation:**
- `-s` = Run as SYSTEM account
- `-i` = Interactive (shows output)

SYSTEM account automatically has `SeTcbPrivilege`, so this will work immediately.

---

## Alternative: Install as Windows Service

For production, install as a Windows Service running as SYSTEM:

```powershell
# Build your application first
dotnet publish -c Release

# Install as service
sc.exe create MyKerberosService binPath= "C:\path\to\your\app.exe"
sc.exe config MyKerberosService obj= "NT AUTHORITY\SYSTEM"
sc.exe start MyKerberosService

# To debug, attach Visual Studio to the service process
```

---

## Verify the Privilege is Enabled

You can verify your account has the privilege:

```powershell
# Check current privileges
whoami /priv

# Look for:
# SeTcbPrivilege    Act as part of the operating system    Disabled
```

If you see `SeTcbPrivilege` in the list (even if Disabled), the application can enable it when running as Administrator.

---

## What the Code Does Now

```csharp
// Before calling LsaRegisterLogonProcess, the code now:
TryEnableSeTcbPrivilege();

// This attempts to:
// 1. Open the current process token
// 2. Look up SeTcbPrivilege
// 3. Enable it in the token
// 4. Silently fails if not possible
```

If the privilege is available (because you granted it), it will be enabled automatically.

---

## Error Messages

### If You Haven't Granted the Privilege:

```
LsaRegisterLogonProcess failed with STATUS_PRIVILEGE_NOT_HELD (0xC0000061).
S4U operations require SeTcbPrivilege.
Solutions:
1. Run as SYSTEM: Install as Windows Service with SYSTEM account
2. Grant privilege: secpol.msc → User Rights Assignment → 'Act as part of the operating system' → Add your account
3. Use psexec: psexec -s -i YourApp.exe
```

### After Granting the Privilege:

The application should work! If you still get the error:
1. Make sure you restarted the application after granting the privilege
2. Make sure you're running as Administrator
3. Try logging out and back in (Windows caches privileges)

---

## Security Warning

⚠️ **`SeTcbPrivilege` is extremely powerful!**

Granting this privilege to your account means:
- You can impersonate any user
- You can bypass many security checks
- You're acting as part of the operating system

**Recommendations:**
- ✅ For development: Grant to your account temporarily
- ✅ For production: Use a dedicated service account
- ❌ Don't grant to regular user accounts
- ❌ Don't leave it enabled on production workstations

---

## Summary

1. ✅ **Grant SeTcbPrivilege** to your account using `secpol.msc`
2. ✅ **Run Visual Studio as Administrator**
3. ✅ **Press F5** - The application will automatically enable the privilege
4. ✅ **S4U operations should now work!**

Or use PsExec to run as SYSTEM without modifying your account privileges.
