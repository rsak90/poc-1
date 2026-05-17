# Run as Windows Service (Quick Steps)

## Step 1: Publish the Application

```powershell
dotnet publish -c Release -o C:\Services\KerberosApp
```

## Step 2: Create the Service

```powershell
# Run PowerShell as Administrator
sc.exe create KerberosService binPath= "C:\Services\KerberosApp\KerberosConstrainedDelegation.exe" start= auto
```

## Step 3: Configure to Run as SYSTEM

```powershell
sc.exe config KerberosService obj= "NT AUTHORITY\SYSTEM"
```

## Step 4: Start the Service

```powershell
sc.exe start KerberosService
```

## Step 5: Check Status

```powershell
sc.exe query KerberosService
```

## View Logs

```powershell
Get-EventLog -LogName Application -Source KerberosService -Newest 10
```

## Stop/Delete Service (If Needed)

```powershell
# Stop
sc.exe stop KerberosService

# Delete
sc.exe delete KerberosService
```

---

## Note

Your app needs to be a Windows Service project. If it's a console app, you'll need to convert it or use `nssm` (Non-Sucking Service Manager):

### Using NSSM (Easier for Console Apps)

```powershell
# Download nssm from https://nssm.cc/download
# Extract to C:\Tools\nssm

C:\Tools\nssm\nssm.exe install KerberosService "C:\Services\KerberosApp\KerberosConstrainedDelegation.exe"
C:\Tools\nssm\nssm.exe set KerberosService ObjectName "NT AUTHORITY\SYSTEM"
C:\Tools\nssm\nssm.exe start KerberosService
```

SYSTEM account has SeTcbPrivilege by default - no need to grant it!
