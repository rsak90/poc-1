# Manual Test Guide for Task 11

## Prerequisites
1. Active Directory domain environment
2. Service account configured for constrained delegation
3. Target user account in Active Directory
4. File share accessible via UNC path
5. FileShareWriter.exe built and available

## Test Scenarios

### Test 1: Successful Execution
**Purpose**: Verify the complete delegation workflow works end-to-end

**Steps**:
1. Configure appsettings.json with valid credentials and paths
2. Run the application: `dotnet run --project src\KerberosConstrainedDelegation`
3. Observe the console output

**Expected Results**:
- Configuration loads successfully
- Configuration validation passes
- Service account authenticates
- Delegation configuration validates
- Delegated token obtained
- Process spawns successfully
- File written to share
- Exit code: 0

**Sample Output**:
```
[2024-01-15 10:30:00.123] Kerberos Constrained Delegation Application
[2024-01-15 10:30:00.124] ============================================
[2024-01-15 10:30:00.125] Loading configuration...
[2024-01-15 10:30:00.150] Validating configuration...
[2024-01-15 10:30:00.151] Configuration validated successfully
[2024-01-15 10:30:00.152] Service Account: CONTOSO\svc_delegation
[2024-01-15 10:30:00.153] Target User: user@contoso.com
[2024-01-15 10:30:00.154] Target SPN: cifs/fileserver.contoso.com
...
[2024-01-15 10:30:02.500] SUCCESS: Delegation and file write completed successfully
```

### Test 2: Invalid Configuration
**Purpose**: Verify configuration validation catches errors

**Steps**:
1. Modify appsettings.json to have missing or invalid values:
   - Remove ServiceAccount.Username
   - Set invalid SPN format
   - Set non-existent executable path
2. Run the application

**Expected Results**:
- Configuration validation fails
- Clear error message displayed
- Exit code: 1

**Sample Output**:
```
[2024-01-15 10:31:00.123] Loading configuration...
[2024-01-15 10:31:00.150] Validating configuration...
ERROR: Configuration validation failed
Details: Service account username is missing (key: ServiceAccount:Username)

Please check your appsettings.json file or command-line arguments.
```

### Test 3: Invalid Service Account Credentials
**Purpose**: Verify authentication error handling

**Steps**:
1. Configure appsettings.json with incorrect password
2. Run the application

**Expected Results**:
- Configuration validates
- Service account authentication fails
- KerberosException thrown
- Troubleshooting guidance displayed
- Exit code: 4

**Sample Output**:
```
KERBEROS ERROR:
===============
Message: Service account authentication failed for CONTOSO\svc_delegation. Win32 error: 0x8009030C
Error Type: ServiceAuthenticationFailed
Win32 Error Code: 0x8009030C (2148073228)

TROUBLESHOOTING GUIDANCE:
-------------------------
- Verify service account credentials (username, domain, password)
- Check if the service account is enabled in Active Directory
- Ensure the service account password has not expired
- Verify network connectivity to Domain Controllers
```

### Test 4: Delegation Not Configured
**Purpose**: Verify delegation configuration validation

**Steps**:
1. Use a service account that is NOT configured for constrained delegation
2. Run the application

**Expected Results**:
- Configuration validates
- Service account authenticates
- Delegation configuration validation fails
- Instructions displayed
- Exit code: 2

**Sample Output**:
```
[2024-01-15 10:32:00.123] Validating delegation configuration...
ERROR: Service account is not properly configured for constrained delegation

TROUBLESHOOTING INSTRUCTIONS:
1. Verify the service account credentials are correct
2. Ensure the service account is configured for constrained delegation in Active Directory
3. Verify the target SPN is in the allowed delegation list for the service account
4. Check that the service account has 'Trust this user for delegation to specified services only' enabled
5. Use 'setspn -L <service_account>' to verify SPN configuration
6. Use 'klist' to view current Kerberos tickets
```

### Test 5: Non-Existent Target User
**Purpose**: Verify user validation error handling

**Steps**:
1. Configure appsettings.json with a non-existent target user
2. Run the application

**Expected Results**:
- Configuration validates
- Service account authenticates
- Delegation configuration validates
- S4U2Self fails with UserNotFound error
- Exit code: 4

**Sample Output**:
```
KERBEROS ERROR:
===============
Message: User not found: nonexistent@contoso.com. NTSTATUS: 0xC0000064
Error Type: UserNotFound
Win32 Error Code: 0x00000525 (1317)

TROUBLESHOOTING GUIDANCE:
-------------------------
- Verify the target username exists in Active Directory
- Check the username format (UPN: user@domain.com or DOMAIN\username)
- Ensure the user account is enabled
```

### Test 6: Process Execution Failure
**Purpose**: Verify process failure handling

**Steps**:
1. Configure FileShareWriter to fail (e.g., invalid UNC path)
2. Run the application

**Expected Results**:
- Delegation succeeds
- Process spawns
- Process returns non-zero exit code
- Error output captured
- Exit code: 3

**Sample Output**:
```
[2024-01-15 10:33:00.123] Process Execution Results:
[2024-01-15 10:33:00.124] Exit Code: 6
[2024-01-15 10:33:00.125] Execution Time: 0.25 seconds
[2024-01-15 10:33:00.126] Timed Out: False

Standard Error:
---------------
ERROR: Failed to write to file share
Details: Access denied to \\fileserver\share\test.txt

ERROR: Process failed with exit code 6
```

### Test 7: Command-Line Arguments Override
**Purpose**: Verify command-line arguments override appsettings.json

**Steps**:
1. Run with command-line arguments:
   ```
   dotnet run --project src\KerberosConstrainedDelegation -- TargetUser:Username=testuser@contoso.com
   ```

**Expected Results**:
- Configuration loads from appsettings.json
- Command-line argument overrides TargetUser.Username
- Application uses testuser@contoso.com instead of value from appsettings.json

### Test 8: Process Timeout
**Purpose**: Verify timeout handling

**Steps**:
1. Set Execution.TimeoutSeconds to a very low value (e.g., 1)
2. Ensure FileShareWriter takes longer than timeout
3. Run the application

**Expected Results**:
- Process spawns
- Process times out
- Process is terminated
- TimedOut flag is true
- Exit code: 3

**Sample Output**:
```
[2024-01-15 10:34:00.123] Process Execution Results:
[2024-01-15 10:34:00.124] Exit Code: 1
[2024-01-15 10:34:00.125] Execution Time: 1.00 seconds
[2024-01-15 10:34:00.126] Timed Out: True

ERROR: Process failed with exit code 1
Process exceeded timeout of 1 seconds
```

## Verification Checklist

After running all tests, verify:
- [ ] All exit codes are correct (0, 1, 2, 3, 4, 5)
- [ ] All error messages are clear and actionable
- [ ] Troubleshooting guidance is helpful
- [ ] Timestamps are present on all log messages
- [ ] Configuration validation catches all invalid inputs
- [ ] Delegation workflow completes successfully with valid configuration
- [ ] Process output is captured correctly
- [ ] Resource cleanup happens (no handle leaks)
- [ ] Application doesn't crash unexpectedly

## Performance Verification

Monitor the following metrics:
- Configuration loading time: < 1 second
- Token acquisition time: < 2 seconds (per requirement 2.1.1)
- Process spawn time: < 1 second (per requirement 2.1.2)
- Total execution time: < 5 seconds for successful run

## Security Verification

Verify:
- [ ] Passwords are not logged to console
- [ ] Token contents are not logged
- [ ] SecureString is used for password storage
- [ ] All resources are disposed properly
- [ ] No sensitive data in error messages

## Notes
- Some tests require Active Directory environment and cannot be run in isolation
- Integration tests should be run in a test environment, not production
- Monitor Windows Event Logs for security audit events during testing
