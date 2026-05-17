# Step-by-Step: Grant SeTcbPrivilege

## The Problem

You're getting `STATUS_PRIVILEGE_NOT_HELD (0xC0000041)` because your account doesn't have `SeTcbPrivilege`, even when running as Administrator.

## Solution: Grant the Privilege

### Step 1: Open Local Security Policy

1. Press `Windows Key + R`
2. Type: `secpol.msc`
3. Press `Enter`

**If you get "MMC could not create the snap-in":**
- You might be on Windows Home edition (doesn't have secpol.msc)
- Skip to "Alternative Method" below

### Step 2: Navigate to User Rights Assignment

1. In the left pane, expand: **Local Policies**
2. Click: **User Rights Assignment**

### Step 3: Find "Act as part of the operating system"

1. In the right pane, scroll down to find: **Act as part of the operating system**
2. Double-click it

### Step 4: Add Your Account

1. Click: **Add User or Group...**
2. Click: **Advanced...**
3. Click: **Find Now**
4. Find and select your username in the list
5. Click: **OK**
6. Click: **OK**
7. Click: **OK** again to close the properties

### Step 5: Log Out and Log Back In

**IMPORTANT:** You MUST log out and log back in for the privilege to take effect!

1. Save all your work
2. Log out of Windows
3. Log back in

### Step 6: Verify

Run PowerShell **as Administrator** and type:
```powershell
whoami /priv | findstr SeTcb
```

You should see:
```
SeTcbPrivilege    Act as part of the operating system    Disabled
```

If you see this line, the privilege is available and the application can enable it!

---

## Alternative Method: Use Registry Editor (Windows Home)

If you don't have `secpol.msc` (Windows Home edition):

### Step 1: Export Current Policy

Run PowerShell **as Administrator**:
```powershell
secedit /export /cfg C:\secpol.cfg
```

### Step 2: Edit the File

1. Open `C:\secpol.cfg` in Notepad
2. Find the line starting with `SeTcbPrivilege`
3. Add your username to the end (format: `*S-1-5-21-...`)

To get your SID:
```powershell
whoami /user
```

Add it like: `SeTcbPrivilege = *S-1-5-21-XXXXXXXXXX-XXXXXXXXXX-XXXXXXXXXX-XXXX`

### Step 3: Import the Policy

```powershell
secedit /configure /db C:\Windows\security\local.sdb /cfg C:\secpol.cfg /areas USER_RIGHTS
```

### Step 4: Log Out and Log Back In

---

## Easiest Alternative: Use PsExec (No Account Modification)

If you don't want to modify your account:

### Step 1: Download PsExec

1. Go to: https://learn.microsoft.com/en-us/sysinternals/downloads/psexec
2. Download and extract to `C:\Tools\PSTools\`

### Step 2: Run Your Application as SYSTEM

Open Command Prompt **as Administrator**:
```cmd
cd C:\Users\USER\OneDrive\Desktop\offline-jobs
C:\Tools\PSTools\psexec.exe -s -i dotnet run
```

**Explanation:**
- `-s` = Run as SYSTEM (which has SeTcbPrivilege by default)
- `-i` = Interactive mode (shows output)

This works immediately without any account modifications!

---

## Verify It's Working

After granting the privilege and logging back in:

1. Open Visual Studio **as Administrator**
2. Run your application (F5)
3. You should NOT see the `STATUS_PRIVILEGE_NOT_HELD` error anymore

If you still get the error:
- Make sure you logged out and back in
- Try restarting your computer
- Verify the privilege with `whoami /priv | findstr SeTcb`

---

## For Production

**DO NOT grant SeTcbPrivilege to regular user accounts in production!**

Instead:
1. Create a dedicated service account
2. Grant SeTcbPrivilege to that service account only
3. Run the application as a Windows Service with that account
4. Configure constrained delegation in Active Directory

---

## Quick Reference

| Method | Pros | Cons |
|--------|------|------|
| **Grant to your account** | Works in Visual Studio | Modifies your account |
| **Use PsExec** | No account modification | Need to download tool |
| **Windows Service** | Production-ready | More complex setup |

Choose the method that works best for your scenario!
