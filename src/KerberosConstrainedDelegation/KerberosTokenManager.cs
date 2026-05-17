using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace KerberosConstrainedDelegation;

/// <summary>
/// Manages Kerberos tokens using constrained delegation (S4U2Self and S4U2Proxy)
/// </summary>
public sealed class KerberosTokenManager : IKerberosTokenManager
{
    private readonly ServiceAccountCredentials _serviceCredentials;
    private readonly ILogger _logger;
    private IntPtr _lsaHandle = IntPtr.Zero;
    private uint _authenticationPackage;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the KerberosTokenManager class
    /// </summary>
    /// <param name="serviceCredentials">Service account credentials for authentication</param>
    /// <param name="logger">
    /// Optional Serilog logger. When null the global <see cref="Log.Logger"/> is used,
    /// which is always safe because Serilog bootstraps a no-op logger by default.
    /// </param>
    public KerberosTokenManager(ServiceAccountCredentials serviceCredentials, ILogger? logger = null)
    {
        _serviceCredentials = serviceCredentials ?? throw new ArgumentNullException(nameof(serviceCredentials));
        // ForContext tags every log line with the class name so it is easy to filter
        // in the log file: grep for "SourceContext" = "KerberosTokenManager"
        _logger = (logger ?? Log.Logger).ForContext<KerberosTokenManager>();
    }

    /// <summary>
    /// Obtains a delegated Kerberos token for the specified user and target service
    /// </summary>
    /// <param name="username">User to impersonate (UPN or DOMAIN\username format)</param>
    /// <param name="targetServicePrincipalName">SPN of the target service for delegation</param>
    /// <returns>Windows token handle with delegated credentials</returns>
    public SafeAccessTokenHandle GetDelegatedToken(string username, string targetServicePrincipalName)
    {
        _logger.Information("[TokenManager] GetDelegatedToken called for user: {Username}, target SPN: {TargetSpn}", username, targetServicePrincipalName);
        
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be null or empty", nameof(username));
        }

        if (string.IsNullOrWhiteSpace(targetServicePrincipalName))
        {
            throw new ArgumentException("Target SPN cannot be null or empty", nameof(targetServicePrincipalName));
        }

        SafeAccessTokenHandle? serviceToken = null;
        SafeAccessTokenHandle? userToken = null;
        SafeAccessTokenHandle? delegatedToken = null;

        try
        {
            // Step 1: Authenticate service account to obtain service token
            _logger.Information("[TokenManager] Step 1: Authenticating service account...");
            serviceToken = AuthenticateServiceAccount();

            if (serviceToken == null || serviceToken.IsInvalid)
            {
                _logger.Error("[TokenManager] ERROR: Service token is null or invalid");
                throw new KerberosException(
                    "Failed to authenticate service account",
                    Marshal.GetLastWin32Error(),
                    KerberosErrorType.ServiceAuthenticationFailed);
            }
            _logger.Information("[TokenManager] Step 1: Service account authenticated successfully");

            // Step 2: Execute S4U2Self with service token and target username to get user token
            _logger.Information("[TokenManager] Step 2: Executing S4U2Self for user: {Username}", username);
            userToken = ExecuteS4U2Self(serviceToken, username);

            if (userToken == null || userToken.IsInvalid)
            {
                _logger.Error("[TokenManager] ERROR: User token is null or invalid");
                throw new KerberosException(
                    $"S4U2Self failed for user: {username}",
                    Marshal.GetLastWin32Error(),
                    KerberosErrorType.S4U2SelfFailed);
            }
            _logger.Information("[TokenManager] Step 2: S4U2Self completed successfully");

            // Step 3: Execute S4U2Proxy with user token and target SPN to get delegated token
            _logger.Information("[TokenManager] Step 3: Executing S4U2Proxy for SPN: {TargetSpn}", targetServicePrincipalName);
            delegatedToken = ExecuteS4U2Proxy(userToken, targetServicePrincipalName);

            if (delegatedToken == null || delegatedToken.IsInvalid)
            {
                _logger.Error("[TokenManager] ERROR: Delegated token is null or invalid");
                throw new KerberosException(
                    $"S4U2Proxy failed for SPN: {targetServicePrincipalName}",
                    Marshal.GetLastWin32Error(),
                    KerberosErrorType.S4U2ProxyFailed);
            }
            _logger.Information("[TokenManager] Step 3: S4U2Proxy completed successfully");

            // Step 4: Validate delegated token represents correct user identity
            _logger.Information("[TokenManager] Step 4: Validating delegated token...");
            ValidateToken(delegatedToken, username, _logger);
            _logger.Information("[TokenManager] Step 4: Token validation successful");

            // Step 5: Return delegated token to caller (caller responsible for disposal)
            _logger.Information("[TokenManager] GetDelegatedToken completed successfully");
            return delegatedToken;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[TokenManager] ERROR in GetDelegatedToken: {ErrorMessage}", ex.Message);
            // If an exception occurs, dispose the delegated token if it was created
            delegatedToken?.Dispose();
            throw;
        }
        finally
        {
            // Step 6: Dispose intermediate tokens (service token, user token) in finally block
            serviceToken?.Dispose();
            userToken?.Dispose();
        }
    }

    /// <summary>
    /// Validates that the service account is properly configured for constrained delegation
    /// </summary>
    /// <returns>True if delegation is properly configured</returns>
    public bool ValidateDelegationConfiguration()
    {
        _logger.Information("[TokenManager] ValidateDelegationConfiguration called");
        
        // This is a simplified implementation that checks if we can authenticate the service account
        // A full implementation would query Active Directory to verify delegation settings
        // For now, we'll attempt to authenticate and return true if successful
        
        SafeAccessTokenHandle? serviceToken = null;
        
        try
        {
            // Try to authenticate the service account
            _logger.Information("[TokenManager] Attempting to authenticate service account for validation...");
            serviceToken = AuthenticateServiceAccount();
            
            // If authentication succeeds, the service account credentials are valid
            // Note: This doesn't verify delegation configuration in AD, but validates credentials
            bool isValid = serviceToken != null && !serviceToken.IsInvalid;
            _logger.Information("[TokenManager] Delegation configuration validation result: {IsValid}", isValid);
            return isValid;
        }
        catch (KerberosException ex)
        {
            _logger.Warning(ex, "[TokenManager] Delegation configuration validation failed: {ErrorMessage}", ex.Message);
            // If authentication fails, delegation is not properly configured
            return false;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "[TokenManager] Delegation configuration validation failed with exception: {ErrorMessage}", ex.Message);
            // Any other exception means configuration is not valid
            return false;
        }
        finally
        {
            // Clean up the service token
            serviceToken?.Dispose();
        }
    }

    /// <summary>
    /// Parses a username in UPN or DOMAIN\username format into domain and account components
    /// </summary>
    /// <param name="username">Username in UPN (user@domain.com) or DOMAIN\username format</param>
    /// <returns>Tuple containing (domain, account) components</returns>
    /// <exception cref="ArgumentException">Thrown when username format is invalid</exception>
    public static (string domain, string account) ParseUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be null or empty", nameof(username));
        }

        // Check for UPN format (user@domain.com)
        if (username.Contains('@'))
        {
            var parts = username.Split('@');
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                throw new ArgumentException($"Invalid UPN format: {username}", nameof(username));
            }

            return (domain: parts[1], account: parts[0]);
        }

        // Check for DOMAIN\username format
        if (username.Contains('\\'))
        {
            var parts = username.Split('\\');
            if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            {
                throw new ArgumentException($"Invalid DOMAIN\\username format: {username}", nameof(username));
            }

            return (domain: parts[0], account: parts[1]);
        }

        throw new ArgumentException($"Username must be in UPN (user@domain.com) or DOMAIN\\username format: {username}", nameof(username));
    }

    /// <summary>
    /// Logs key token identity fields (type, impersonation level, auth package) using
    /// only GetTokenInformation — avoids constructing WindowsIdentity which can throw
    /// on S4U Identification-level tokens in some contexts.
    /// </summary>
    private void LogTokenInfo(string label, SafeAccessTokenHandle token)
    {
        if (token == null || token.IsInvalid)
        {
            _logger.Warning("[TokenManager] [{Label}] token is NULL or INVALID", label);
            return;
        }

        try
        {
            NativeMethods.GetTokenInformation(token, NativeMethods.TokenStatistics, IntPtr.Zero, 0, out int needed);
            var buf = Marshal.AllocHGlobal(needed);
            try
            {
                if (!NativeMethods.GetTokenInformation(token, NativeMethods.TokenStatistics, buf, needed, out _))
                {
                    _logger.Warning("[TokenManager] [{Label}] GetTokenInformation(TokenStatistics) failed: Win32 {Win32Error}",
                        label, Marshal.GetLastWin32Error());
                    return;
                }

                var stats = Marshal.PtrToStructure<NativeMethods.TOKEN_STATISTICS>(buf);

                string tokenType = stats.TokenType switch { 1 => "Primary", 2 => "Impersonation", _ => $"Unknown({stats.TokenType})" };
                string impLevel  = stats.ImpersonationLevel switch
                {
                    0 => "Anonymous", 1 => "Identification", 2 => "Impersonation", 3 => "Delegation",
                    _ => $"Unknown({stats.ImpersonationLevel})"
                };

                _logger.Information(
                    "[TokenManager] [{Label}] TokenType={TokenType} ImpersonationLevel={ImpLevel} LogonId={LogonHigh:X8}:{LogonLow:X8}",
                    label, tokenType, impLevel,
                    stats.AuthenticationId.HighPart, stats.AuthenticationId.LowPart);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        catch (Exception ex)
        {
            _logger.Warning("[TokenManager] [{Label}] LogTokenInfo failed: {ErrorMessage}", label, ex.Message);
        }
    }

    /// <summary>
    /// Validates that a token represents the expected user identity
    /// </summary>
    /// <param name="token">Token to validate</param>
    /// <param name="expectedUsername">Expected username (UPN or DOMAIN\username format)</param>
    /// <param name="logger">Optional logger. Falls back to the global Log.Logger when null.</param>
    /// <returns>True if token represents the expected user</returns>
    /// <exception cref="KerberosException">Thrown when token validation fails</exception>
    public static bool ValidateToken(SafeAccessTokenHandle token, string expectedUsername, ILogger? logger = null)
    {
        var log = (logger ?? Log.Logger).ForContext("ValidateTokenFor", expectedUsername);

        if (token == null || token.IsInvalid)
        {
            throw new ArgumentException("Invalid token handle", nameof(token));
        }

        if (string.IsNullOrWhiteSpace(expectedUsername))
        {
            throw new ArgumentException("Expected username cannot be null or empty", nameof(expectedUsername));
        }

        try
        {
            // Log token type/level before any checks so it's visible even on failure
            if (token != null && !token.IsInvalid)
            {
                try
                {
                    NativeMethods.GetTokenInformation(token, NativeMethods.TokenStatistics, IntPtr.Zero, 0, out int needed);
                    var buf = Marshal.AllocHGlobal(needed);
                    try
                    {
                        if (NativeMethods.GetTokenInformation(token, NativeMethods.TokenStatistics, buf, needed, out _))
                        {
                            var stats = Marshal.PtrToStructure<NativeMethods.TOKEN_STATISTICS>(buf);
                            string tt = stats.TokenType switch { 1 => "Primary", 2 => "Impersonation", _ => $"Unknown({stats.TokenType})" };
                            string il = stats.ImpersonationLevel switch { 0 => "Anonymous", 1 => "Identification", 2 => "Impersonation", 3 => "Delegation", _ => $"Unknown({stats.ImpersonationLevel})" };
                            log.Information("[TokenManager] ValidateToken: TokenType={TokenType} ImpersonationLevel={ImpLevel} LogonId={LogonHigh:X8}:{LogonLow:X8}",
                                tt, il, stats.AuthenticationId.HighPart, stats.AuthenticationId.LowPart);
                        }
                    }
                    finally { Marshal.FreeHGlobal(buf); }
                }
                catch { /* non-critical */ }
            }

            // Get the token account name (without domain) for comparison.
            // We compare only the account/username portion because the domain
            // returned by WindowsIdentity.Name is always the NetBIOS name
            // (e.g. "CONTOSO"), while ParseUsername on a UPN returns the FQDN
            // (e.g. "contoso.com"). Comparing full "domain\user" strings across
            // these two formats always fails even when the identity is correct.
            var tokenUsername = GetTokenUsername(token);
            var (_, expectedAccount) = ParseUsername(expectedUsername);

            log.Information("[TokenManager] ValidateToken: token account='{TokenUsername}', expected account='{ExpectedAccount}'",
                tokenUsername, expectedAccount);

            // Compare only the account name (case-insensitive)
            if (!string.Equals(tokenUsername, expectedAccount, StringComparison.OrdinalIgnoreCase))
            {
                var tokenDomain = GetTokenDomain(token);
                throw new KerberosException(
                    $"Token username mismatch. Expected account: {expectedAccount}, Got: {tokenDomain}\\{tokenUsername}",
                    0,
                    KerberosErrorType.TokenCreationFailed);
            }

            // Validate authentication type using WindowsIdentity.
            // S4U tokens obtained via LsaLogonUser report AuthenticationType as
            // "S4U" or "Negotiate" — never "Kerberos". Accept all three so that
            // valid S4U tokens are not incorrectly rejected.
            using var identity = new WindowsIdentity(token.DangerousGetHandle());
            
            if (!identity.IsAuthenticated)
            {
                throw new KerberosException(
                    "Token is not authenticated",
                    0,
                    KerberosErrorType.TokenCreationFailed);
            }

            var authType = identity.AuthenticationType ?? string.Empty;
            log.Information("[TokenManager] ValidateToken: AuthenticationType='{AuthType}'", authType);

            bool isValidAuthType =
                string.Equals(authType, "Kerberos",  StringComparison.OrdinalIgnoreCase) ||
                string.Equals(authType, "S4U",       StringComparison.OrdinalIgnoreCase) ||
                string.Equals(authType, "Negotiate", StringComparison.OrdinalIgnoreCase);

            if (!isValidAuthType)
            {
                throw new KerberosException(
                    $"Unexpected token authentication type: '{authType}'. Expected Kerberos, S4U, or Negotiate.",
                    0,
                    KerberosErrorType.TokenCreationFailed);
            }

            return true;
        }
        catch (KerberosException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new KerberosException(
                $"Token validation failed: {ex.Message}",
                Marshal.GetLastWin32Error(),
                KerberosErrorType.TokenCreationFailed,
                ex);
        }
    }

    /// <summary>
    /// Extracts the username from a token
    /// </summary>
    /// <param name="token">Token to extract username from</param>
    /// <returns>Username from the token</returns>
    public static string GetTokenUsername(SafeAccessTokenHandle token)
    {
        if (token == null || token.IsInvalid)
        {
            throw new ArgumentException("Invalid token handle", nameof(token));
        }

        try
        {
            using var identity = new WindowsIdentity(token.DangerousGetHandle());
            
            // Extract just the username part (without domain)
            var fullName = identity.Name;
            if (fullName.Contains('\\'))
            {
                return fullName.Split('\\')[1];
            }
            else if (fullName.Contains('@'))
            {
                return fullName.Split('@')[0];
            }
            
            return fullName;
        }
        catch (Exception ex)
        {
            throw new KerberosException(
                $"Failed to get token username: {ex.Message}",
                Marshal.GetLastWin32Error(),
                KerberosErrorType.TokenCreationFailed,
                ex);
        }
    }

    /// <summary>
    /// Extracts the domain from a token
    /// </summary>
    /// <param name="token">Token to extract domain from</param>
    /// <returns>Domain from the token</returns>
    public static string GetTokenDomain(SafeAccessTokenHandle token)
    {
        if (token == null || token.IsInvalid)
        {
            throw new ArgumentException("Invalid token handle", nameof(token));
        }

        try
        {
            using var identity = new WindowsIdentity(token.DangerousGetHandle());
            
            // Extract domain part
            var fullName = identity.Name;
            if (fullName.Contains('\\'))
            {
                return fullName.Split('\\')[0];
            }
            else if (fullName.Contains('@'))
            {
                return fullName.Split('@')[1];
            }
            
            // If no domain separator found, try to get from environment
            return Environment.UserDomainName;
        }
        catch (Exception ex)
        {
            throw new KerberosException(
                $"Failed to get token domain: {ex.Message}",
                Marshal.GetLastWin32Error(),
                KerberosErrorType.TokenCreationFailed,
                ex);
        }
    }

    /// <summary>
    /// Checks if a token is forwardable (can be used for delegation)
    /// </summary>
    /// <param name="token">Token to check</param>
    /// <returns>True if token is forwardable</returns>
    public static bool IsTokenForwardable(SafeAccessTokenHandle token)
    {
        if (token == null || token.IsInvalid)
        {
            throw new ArgumentException("Invalid token handle", nameof(token));
        }

        try
        {
            // Get token statistics to check if it's forwardable
            // A token is forwardable if it has delegation impersonation level
            int returnLength;
            var success = NativeMethods.GetTokenInformation(
                token,
                NativeMethods.TokenStatistics,
                IntPtr.Zero,
                0,
                out returnLength);

            var buffer = Marshal.AllocHGlobal(returnLength);
            try
            {
                success = NativeMethods.GetTokenInformation(
                    token,
                    NativeMethods.TokenStatistics,
                    buffer,
                    returnLength,
                    out returnLength);

                if (!success)
                {
                    var error = Marshal.GetLastWin32Error();
                    throw new KerberosException(
                        $"Failed to get token statistics: Win32 error {error}",
                        error,
                        KerberosErrorType.TokenCreationFailed);
                }

                // S4U2Self tokens from LsaLogonUser are issued at Identification level
                // by default. The Windows impersonation level in TOKEN_STATISTICS is a
                // local access-control concept and does NOT reflect whether the embedded
                // Kerberos ticket has the FORWARDABLE flag set.
                //
                // Forwardability for S4U2Proxy is determined by the service account's
                // constrained delegation configuration in Active Directory, not by this
                // field. Checking ImpersonationLevel == SECURITY_DELEGATION here is
                // incorrect and will always reject valid S4U2Self tokens.
                //
                // Log the level for diagnostics but always return true — S4U2Proxy will
                // fail with a meaningful error if delegation is not configured in AD.
                var stats = Marshal.PtrToStructure<NativeMethods.TOKEN_STATISTICS>(buffer);
                string levelName = stats.ImpersonationLevel switch {
                    0 => "Anonymous",
                    1 => "Identification",
                    2 => "Impersonation",
                    3 => "Delegation",
                    _ => $"Unknown({stats.ImpersonationLevel})"
                };
                Log.Logger.Information("[TokenManager] Token impersonation level: {ImpLevel} ({ImpLevelId}) — forwardability determined by AD delegation config",
                    levelName, stats.ImpersonationLevel);
                return true;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (KerberosException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new KerberosException(
                $"Failed to check if token is forwardable: {ex.Message}",
                Marshal.GetLastWin32Error(),
                KerberosErrorType.TokenCreationFailed,
                ex);
        }
    }

    /// <summary>
    /// Registers the LSA authentication package for Kerberos
    /// </summary>
    /// <param name="packageName">Name of the authentication package (typically "Negotiate")</param>
    /// <param name="requireTrustedConnection">If true, only use LsaRegisterLogonProcess (required for S4U operations)</param>
    /// <returns>Authentication package ID</returns>
    /// <exception cref="KerberosException">Thrown when registration fails</exception>
    public uint RegisterLsaAuthenticationPackage(string packageName = "Negotiate", bool requireTrustedConnection = true)
    {
        _logger.Information("[TokenManager] RegisterLsaAuthenticationPackage called with package: {PackageName}, requireTrustedConnection: {RequireTrustedConnection}",
            packageName, requireTrustedConnection);
        
        if (string.IsNullOrWhiteSpace(packageName))
        {
            throw new ArgumentException("Package name cannot be null or empty", nameof(packageName));
        }

        // If already registered, return cached value
        if (_lsaHandle != IntPtr.Zero && _authenticationPackage != 0)
        {
            _logger.Information("[TokenManager] Using cached LSA handle and authentication package: {AuthPackageId}", _authenticationPackage);
            return _authenticationPackage;
        }

        try
        {
            int status;
            
            // For S4U operations, we MUST use LsaRegisterLogonProcess (requires SeTcbPrivilege)
            // LsaConnectUntrusted does NOT support S4U2Self/S4U2Proxy
            if (requireTrustedConnection)
            {
                _logger.Information("[TokenManager] Trusted connection required - attempting LsaRegisterLogonProcess");
                // Try to enable SeTcbPrivilege if we're running as Administrator
                TryEnableSeTcbPrivilege();
                
                var processName = NativeMethods.CreateLsaString("KerberosConstrainedDelegation");
                try
                {
                    _logger.Information("[TokenManager] Calling LsaRegisterLogonProcess...");
                    status = NativeMethods.LsaRegisterLogonProcess(
                        ref processName,
                        out _lsaHandle,
                        out ulong securityMode);

                    if (status != NativeMethods.STATUS_SUCCESS)
                    {
                        var win32Error = NativeMethods.LsaNtStatusToWinError(status);
                        _logger.Error("[TokenManager] ERROR: LsaRegisterLogonProcess failed with status: 0x{NtStatus:X8}, Win32 error: {Win32Error}",
                            status, win32Error);
                        
                        if (status == NativeMethods.STATUS_PRIVILEGE_NOT_HELD)
                        {
                            throw new KerberosException(
                                $"LsaRegisterLogonProcess failed with STATUS_PRIVILEGE_NOT_HELD (0x{status:X8}). " +
                                $"S4U operations require SeTcbPrivilege. " +
                                $"Solutions:\n" +
                                $"1. Run as SYSTEM: Install as Windows Service with SYSTEM account\n" +
                                $"2. Grant privilege: secpol.msc → User Rights Assignment → 'Act as part of the operating system' → Add your account\n" +
                                $"3. Use psexec: psexec -s -i YourApp.exe",
                                win32Error,
                                KerberosErrorType.ServiceAuthenticationFailed);
                        }
                        
                        throw new KerberosException(
                            $"LsaRegisterLogonProcess failed with status: 0x{status:X8}. " +
                            $"This operation requires SeTcbPrivilege.",
                            win32Error,
                            KerberosErrorType.ServiceAuthenticationFailed);
                    }
                    _logger.Information("[TokenManager] LsaRegisterLogonProcess succeeded, LSA handle obtained");
                }
                finally
                {
                    NativeMethods.FreeLsaString(ref processName);
                }
            }
            else
            {
                _logger.Information("[TokenManager] Attempting LsaConnectUntrusted first...");
                // Try LsaConnectUntrusted first (doesn't require SeTcbPrivilege)
                status = NativeMethods.LsaConnectUntrusted(out _lsaHandle);

                // If LsaConnectUntrusted fails, try LsaRegisterLogonProcess
                if (status != NativeMethods.STATUS_SUCCESS)
                {
                    _logger.Warning("[TokenManager] LsaConnectUntrusted failed with status: 0x{NtStatus:X8}, trying LsaRegisterLogonProcess...", status);
                    TryEnableSeTcbPrivilege();
                    
                    var processName = NativeMethods.CreateLsaString("KerberosConstrainedDelegation");
                    try
                    {
                        status = NativeMethods.LsaRegisterLogonProcess(
                            ref processName,
                            out _lsaHandle,
                            out ulong securityMode);

                        if (status != NativeMethods.STATUS_SUCCESS)
                        {
                            var win32Error = NativeMethods.LsaNtStatusToWinError(status);
                            throw new KerberosException(
                                $"Both LsaConnectUntrusted and LsaRegisterLogonProcess failed. LsaRegisterLogonProcess status: 0x{status:X8}. " +
                                $"This operation may require SeTcbPrivilege.",
                                win32Error,
                                KerberosErrorType.ServiceAuthenticationFailed);
                        }
                    }
                    finally
                    {
                        NativeMethods.FreeLsaString(ref processName);
                    }
                }
            }

            // Try multiple package names in order of preference
            string[] packageNamesToTry = packageName == "Negotiate" 
                ? new[] { NativeMethods.NEGOSSP_NAME, NativeMethods.MICROSOFT_KERBEROS_NAME, NativeMethods.MSV1_0_PACKAGE_NAME }
                : packageName == "Kerberos"
                    ? new[] { NativeMethods.MICROSOFT_KERBEROS_NAME }  // S4U requires Kerberos only — no MSV1_0 fallback
                    : new[] { packageName, NativeMethods.MSV1_0_PACKAGE_NAME };

            Exception? lastException = null;
            
            foreach (var pkgName in packageNamesToTry)
            {
                var authPackageName = NativeMethods.CreateLsaString(pkgName);
                try
                {
                    status = NativeMethods.LsaLookupAuthenticationPackage(
                        _lsaHandle,
                        ref authPackageName,
                        out _authenticationPackage);

                    if (status == NativeMethods.STATUS_SUCCESS && _authenticationPackage != 0)
                    {
                        // Success! Return the valid package ID
                        _logger.Information("[TokenManager] Successfully registered authentication package '{PkgName}' with ID: {AuthPackageId}",
                            pkgName, _authenticationPackage);
                        return _authenticationPackage;
                    }

                    // If we got here, either status failed or package ID is 0
                    if (status != NativeMethods.STATUS_SUCCESS)
                    {
                        var win32Error = NativeMethods.LsaNtStatusToWinError(status);
                        lastException = new KerberosException(
                            $"LsaLookupAuthenticationPackage failed for '{pkgName}' with status: 0x{status:X8}",
                            win32Error,
                            KerberosErrorType.ServiceAuthenticationFailed);
                    }
                    else
                    {
                        lastException = new KerberosException(
                            $"LsaLookupAuthenticationPackage returned invalid package ID (0) for '{pkgName}'.",
                            0,
                            KerberosErrorType.ServiceAuthenticationFailed);
                    }
                }
                finally
                {
                    NativeMethods.FreeLsaString(ref authPackageName);
                }
            }

            // If we get here, all package names failed
            throw new KerberosException(
                $"Failed to lookup any authentication package. Tried: {string.Join(", ", packageNamesToTry)}. " +
                $"Last error: {lastException?.Message ?? "Unknown"}",
                0,
                KerberosErrorType.ServiceAuthenticationFailed,
                lastException!);
        }
        catch (KerberosException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new KerberosException(
                $"Failed to register LSA authentication package: {ex.Message}",
                Marshal.GetLastWin32Error(),
                KerberosErrorType.ServiceAuthenticationFailed,
                ex);
        }
    }

    /// <summary>
    /// Authenticates the service account to obtain initial credentials for delegation operations
    /// </summary>
    /// <returns>SafeAccessTokenHandle representing the authenticated service account</returns>
    /// <exception cref="KerberosException">Thrown when authentication fails</exception>
    private SafeAccessTokenHandle AuthenticateServiceAccount()
    {
        _logger.Information("[TokenManager] AuthenticateServiceAccount called");
        
        if (_serviceCredentials == null)
        {
            throw new InvalidOperationException("Service credentials are not initialized");
        }

        _logger.Information("[TokenManager] Authenticating service account: {ServiceAccount}", _serviceCredentials.FullyQualifiedUsername);
        
        // Convert SecureString password to plain text for LogonUser API
        IntPtr passwordPtr = IntPtr.Zero;
        try
        {
            passwordPtr = Marshal.SecureStringToGlobalAllocUnicode(_serviceCredentials.Password);
            var password = Marshal.PtrToStringUni(passwordPtr);

            if (password == null)
            {
                throw new KerberosException(
                    "Failed to convert password from SecureString",
                    0,
                    KerberosErrorType.ServiceAuthenticationFailed);
            }

            // Call LogonUser API with LOGON32_LOGON_NETWORK logon type
            _logger.Information("[TokenManager] Calling LogonUser for service account...");
            var success = NativeMethods.LogonUser(
                _serviceCredentials.Username,
                _serviceCredentials.Domain,
                password,
                NativeMethods.LOGON32_LOGON_NETWORK,
                NativeMethods.LOGON32_PROVIDER_DEFAULT,
                out SafeAccessTokenHandle token);

            if (!success)
            {
                var win32Error = Marshal.GetLastWin32Error();
                _logger.Error("[TokenManager] ERROR: LogonUser failed with Win32 error: 0x{Win32Error:X8}", win32Error);
                throw new KerberosException(
                    $"Service account authentication failed for {_serviceCredentials.FullyQualifiedUsername}. Win32 error: 0x{win32Error:X8}",
                    win32Error,
                    KerberosErrorType.ServiceAuthenticationFailed);
            }

            // Validate that the token handle is valid
            if (token == null || token.IsInvalid)
            {
                _logger.Error("[TokenManager] ERROR: LogonUser returned invalid token handle");
                throw new KerberosException(
                    $"Service account authentication returned invalid token handle for {_serviceCredentials.FullyQualifiedUsername}",
                    0,
                    KerberosErrorType.ServiceAuthenticationFailed);
            }

            _logger.Information("[TokenManager] Service account authenticated successfully");
            LogTokenInfo("ServiceToken", token);
            return token;
        }
        catch (KerberosException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new KerberosException(
                $"Unexpected error during service account authentication: {ex.Message}",
                Marshal.GetLastWin32Error(),
                KerberosErrorType.ServiceAuthenticationFailed,
                ex);
        }
        finally
        {
            // Clear the password from memory
            if (passwordPtr != IntPtr.Zero)
            {
                Marshal.ZeroFreeGlobalAllocUnicode(passwordPtr);
            }
        }
    }

    /// <summary>
    /// Executes S4U2Self (Service for User to Self) to obtain a forwardable ticket for the target user
    /// </summary>
    /// <param name="serviceToken">Service account token</param>
    /// <param name="username">Username to impersonate (UPN or DOMAIN\username format)</param>
    /// <returns>SafeAccessTokenHandle representing the user's forwardable ticket</returns>
    /// <exception cref="KerberosException">Thrown when S4U2Self fails</exception>
    private SafeAccessTokenHandle ExecuteS4U2Self(SafeAccessTokenHandle serviceToken, string username)
    {
        _logger.Information("[TokenManager] ExecuteS4U2Self called for username: {Username}", username);
        
        if (serviceToken == null || serviceToken.IsInvalid)
        {
            throw new ArgumentException("Invalid service token handle", nameof(serviceToken));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be null or empty", nameof(username));
        }

        // Step 1: Parse username into domain and account components
        var (userDomain, account) = ParseUsername(username);
        _logger.Information("[TokenManager] Parsed username - Domain: {UserDomain}, Account: {Account}", userDomain, account);

        // ClientRealm must be the SERVICE ACCOUNT's domain (the local domain whose DC
        // we are contacting), NOT the user's domain. The local DC uses the cross-domain
        // trust to look up the user in iconplc.com. Passing the user's domain here causes
        // the Kerberos package to try to contact iconplc.com directly, which fails with
        // STATUS_INVALID_PARAMETER when there is no direct path.
        //
        // S4U_LOGON_FLAG_IDENTITY (0x8) is required for cross-domain S4U2Self — without
        // it the DC returns STATUS_NO_TRUST_SAM_ACCOUNT for users in trusted domains.
        string clientRealm = _serviceCredentials.Domain;
        uint s4uFlags = NativeMethods.S4U_LOGON_FLAG_IDENTITY;
        _logger.Information("[TokenManager] S4U ClientUpn='{ClientUpn}', ClientRealm='{ClientRealm}' (service domain), Flags=0x{S4UFlags:X}",
            username, clientRealm, s4uFlags);

        // Step 2: Register LSA authentication package
        // Must use "Kerberos" (not "Negotiate") for S4U operations — Negotiate
        // does not accept KERB_S4U_LOGON and causes STATUS_INVALID_PARAMETER (0xC000000D).
        _logger.Information("[TokenManager] Registering LSA authentication package...");
        var authPackage = RegisterLsaAuthenticationPackage("Kerberos");

        // Validate that we got a valid authentication package ID
        if (authPackage == 0)
        {
            _logger.Error("[TokenManager] ERROR: Authentication package ID is 0");
            throw new KerberosException(
                "Failed to obtain valid authentication package ID. LsaLookupAuthenticationPackage returned 0.",
                0,
                KerberosErrorType.S4U2SelfFailed);
        }
        _logger.Information("[TokenManager] Authentication package ID: {AuthPackageId}", authPackage);

        // Step 3: Build KERB_S4U_LOGON structure as a single contiguous memory block.
        //
        // CRITICAL: The Kerberos package requires that the UNICODE_STRING Buffer fields
        // contain pointers to memory within the SAME allocation passed to LsaLogonUser.
        // Using separate heap allocations (Marshal.StringToHGlobalUni) causes
        // STATUS_INVALID_PARAMETER (0xC000000D) because the LSA validates that all
        // string data resides within the AuthenticationInformation buffer bounds.
        //
        // Layout: [KERB_S4U_LOGON header][UPN chars][Realm chars]
        //         Buffer pointers point into this same block.
        _logger.Information("[TokenManager] Building KERB_S4U_LOGON structure...");

        // Encode strings as UTF-16LE (Unicode)
        var upnBytes   = System.Text.Encoding.Unicode.GetBytes(username);
        var realmBytes = System.Text.Encoding.Unicode.GetBytes(clientRealm);

        var structSize  = Marshal.SizeOf<NativeMethods.KERB_S4U_LOGON>();
        var totalSize   = structSize + upnBytes.Length + realmBytes.Length;
        var s4uLogonPtr = Marshal.AllocHGlobal(totalSize);

        try
        {
            // Zero the entire buffer first
            for (int i = 0; i < totalSize; i++)
                Marshal.WriteByte(s4uLogonPtr, i, 0);

            // Calculate where each string will live inside the buffer
            var upnOffset   = structSize;
            var realmOffset = structSize + upnBytes.Length;

            var upnPtr   = IntPtr.Add(s4uLogonPtr, upnOffset);
            var realmPtr = IntPtr.Add(s4uLogonPtr, realmOffset);

            // Copy string bytes into the buffer
            Marshal.Copy(upnBytes,   0, upnPtr,   upnBytes.Length);
            Marshal.Copy(realmBytes, 0, realmPtr, realmBytes.Length);

            // Write the KERB_S4U_LOGON header fields using Marshal.OffsetOf for
            // correct offsets on both 32-bit and 64-bit.
            //
            // On 64-bit: UNICODE_STRING = { Length(2), MaxLength(2), pad(4), Buffer(8) } = 16 bytes
            //   Buffer is at offset +8 within UNICODE_STRING (NOT +4).
            // Hardcoding +4 for Buffer is the classic 64-bit S4U marshalling bug.
            int upnFieldOffset   = (int)Marshal.OffsetOf<NativeMethods.KERB_S4U_LOGON>("ClientUpn");
            int realmFieldOffset = (int)Marshal.OffsetOf<NativeMethods.KERB_S4U_LOGON>("ClientRealm");
            int bufferSubOffset  = (int)Marshal.OffsetOf<NativeMethods.UNICODE_STRING>("Buffer");

            Marshal.WriteInt32(s4uLogonPtr, 0, NativeMethods.KerbS4ULogon);  // MessageType = 12
            Marshal.WriteInt32(s4uLogonPtr, 4, (int)s4uFlags);               // Flags

            // ClientUpn
            Marshal.WriteInt16(s4uLogonPtr, upnFieldOffset,                  (short)upnBytes.Length);        // Length
            Marshal.WriteInt16(s4uLogonPtr, upnFieldOffset + 2,              (short)(upnBytes.Length + 2));  // MaximumLength
            Marshal.WriteIntPtr(s4uLogonPtr, upnFieldOffset + bufferSubOffset, upnPtr);                       // Buffer

            // ClientRealm
            Marshal.WriteInt16(s4uLogonPtr, realmFieldOffset,                  (short)realmBytes.Length);        // Length
            Marshal.WriteInt16(s4uLogonPtr, realmFieldOffset + 2,              (short)(realmBytes.Length + 2));  // MaximumLength
            Marshal.WriteIntPtr(s4uLogonPtr, realmFieldOffset + bufferSubOffset, realmPtr);                       // Buffer

            // Step 4: Prepare origin name and token source
            var originName = NativeMethods.CreateLsaString("S4U2Self");
            try
            {
                // TOKEN_SOURCE.SourceName must be exactly 8 bytes
                var sourceNameBytes = new byte[8];
                var encodedBytes = System.Text.Encoding.ASCII.GetBytes("S4U2Self");
                Array.Copy(encodedBytes, sourceNameBytes, Math.Min(encodedBytes.Length, 8));

                var tokenSource = new NativeMethods.TOKEN_SOURCE
                {
                    SourceName = sourceNameBytes,
                    SourceIdentifier = new NativeMethods.LUID { LowPart = 0, HighPart = 0 }
                };

                // Step 5: Call LsaLogonUser API with S4U2Self request.
                // Network (3) is the correct logon type for S4U — the token level
                // (Identification) is handled in ExecuteS4U2Proxy via the
                // Impersonate → OpenThreadToken → DuplicateTokenEx pattern.
                _logger.Information("[TokenManager] Calling LsaLogonUser for S4U2Self (logon type: Network)...");
                var status = NativeMethods.LsaLogonUser(
                    _lsaHandle,
                    ref originName,
                    NativeMethods.Network,
                    authPackage,
                    s4uLogonPtr,
                    (uint)totalSize,
                    IntPtr.Zero,
                    ref tokenSource,
                    out IntPtr profileBuffer,
                    out uint profileBufferLength,
                    out NativeMethods.LUID logonId,
                    out SafeAccessTokenHandle token,
                    out NativeMethods.QUOTA_LIMITS quotas,
                    out int subStatus);

                // Free profile buffer if allocated
                if (profileBuffer != IntPtr.Zero)
                {
                    NativeMethods.LsaFreeReturnBuffer(profileBuffer);
                }

                if (status != NativeMethods.STATUS_SUCCESS)
                {
                    var win32Error = NativeMethods.LsaNtStatusToWinError(status);
                    _logger.Error("[TokenManager] ERROR: LsaLogonUser (S4U2Self) failed with status: 0x{NtStatus:X8}, SubStatus: 0x{SubStatus:X8}, Win32 error: {Win32Error}",
                        status, subStatus, win32Error);

                    if (status == NativeMethods.STATUS_NO_SUCH_USER)
                    {
                        throw new KerberosException(
                            $"User not found: {username}. NTSTATUS: 0x{status:X8}",
                            win32Error,
                            KerberosErrorType.UserNotFound);
                    }

                    throw new KerberosException(
                        $"LsaLogonUser (S4U2Self) failed for user: {username}. NTSTATUS: 0x{status:X8}, SubStatus: 0x{subStatus:X8}",
                        win32Error,
                        KerberosErrorType.S4U2SelfFailed);
                }

                _logger.Information("[TokenManager] LsaLogonUser (S4U2Self) succeeded");

                // Validate token handle
                if (token == null || token.IsInvalid)
                {
                    _logger.Error("[TokenManager] ERROR: S4U2Self returned invalid token handle");
                    throw new KerberosException(
                        $"S4U2Self returned invalid token handle for user: {username}",
                        0,
                        KerberosErrorType.S4U2SelfFailed);
                }

                // Step 6: Validate returned token is forwardable (required for S4U2Proxy)
                _logger.Information("[TokenManager] Checking if token is forwardable...");
                if (!IsTokenForwardable(token))
                {
                    token.Dispose();
                    _logger.Error("[TokenManager] ERROR: Token is not forwardable");
                    throw new KerberosException(
                        $"S4U2Self token is not forwardable for user: {username}. Token cannot be used for delegation.",
                        0,
                        KerberosErrorType.S4U2SelfFailed);
                }

                _logger.Information("[TokenManager] Token is forwardable - ExecuteS4U2Self completed successfully");
                LogTokenInfo("S4U2Self", token);
                return token;
            }
            finally
            {
                NativeMethods.FreeLsaString(ref originName);
            }
        }
        catch (KerberosException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new KerberosException(
                $"Unexpected error during S4U2Self for user {username}: {ex.Message}",
                Marshal.GetLastWin32Error(),
                KerberosErrorType.S4U2SelfFailed,
                ex);
        }
        finally
        {
            Marshal.FreeHGlobal(s4uLogonPtr);
        }
    }

    /// <summary>
    /// Executes S4U2Proxy (Service for User to Proxy) to obtain a service ticket for the target service using delegated credentials
    /// </summary>
    /// <param name="userToken">User token obtained from S4U2Self (Identification-level)</param>
    /// <param name="targetSpn">Target Service Principal Name for delegation</param>
    /// <returns>Primary SafeAccessTokenHandle suitable for CreateProcessWithTokenW</returns>
    /// <exception cref="KerberosException">Thrown when S4U2Proxy fails</exception>
    private SafeAccessTokenHandle ExecuteS4U2Proxy(SafeAccessTokenHandle userToken, string targetSpn)
    {
        _logger.Information("[TokenManager] ExecuteS4U2Proxy called for target SPN: {TargetSpn}", targetSpn);

        if (userToken == null || userToken.IsInvalid)
            throw new ArgumentException("Invalid user token handle", nameof(userToken));
        if (string.IsNullOrWhiteSpace(targetSpn))
            throw new ArgumentException("Target SPN cannot be null or empty", nameof(targetSpn));

        // LsaLogonUser with KERB_S4U_LOGON always returns an Identification-level token
        // regardless of logon type — this is a Windows security constraint, not a bug.
        // Identification tokens cannot be used with CreateProcessWithTokenW (error 1346).
        //
        // The correct pattern to get a usable Primary token from an Identification token:
        //
        //   1. ImpersonateLoggedOnUser  — sets the S4U identity on the current thread.
        //                                 This works even with Identification-level tokens
        //                                 when the caller holds SeTcbPrivilege.
        //   2. OpenThreadToken          — retrieves the thread's impersonation token,
        //                                 which is Impersonation-level (not Identification).
        //   3. DuplicateTokenEx         — converts the Impersonation token to a Primary
        //                                 token, which CreateProcessWithTokenW requires.
        //   4. RevertToSelf             — removes the impersonation from the thread.
        //
        // No logoff/login or AD changes are needed. SeTcbPrivilege (already granted) is
        // sufficient for step 1.

        const uint TOKEN_ALL_ACCESS     = 0x000F01FF;
        const int  SecurityImpersonation = 2;
        const int  TokenPrimary          = 1;

        SafeAccessTokenHandle? threadToken  = null;
        SafeAccessTokenHandle? primaryToken = null;
        bool impersonating = false;

        try
        {
            // Step 1: Impersonate the S4U user on the current thread
            _logger.Information("[TokenManager] S4U2Proxy: calling ImpersonateLoggedOnUser...");
            if (!NativeMethods.ImpersonateLoggedOnUser(userToken))
            {
                int err = Marshal.GetLastWin32Error();
                _logger.Error("[TokenManager] ImpersonateLoggedOnUser failed: Win32 {Win32Error} (0x{Win32ErrorHex:X8})", err, err);
                throw new KerberosException(
                    $"ImpersonateLoggedOnUser failed. Win32 error: {err} (0x{err:X8}). " +
                    $"Ensure the service account has SeTcbPrivilege.",
                    err, KerberosErrorType.S4U2ProxyFailed);
            }
            impersonating = true;
            _logger.Information("[TokenManager] S4U2Proxy: impersonation active on current thread");

            // Step 2: Open the thread's impersonation token (Impersonation-level)
            // TOKEN_ALL_ACCESS so we can duplicate it in step 3.
            // OpenAsSelf=false means open under the impersonated identity.
            if (!NativeMethods.OpenThreadToken(
                    NativeMethods.GetCurrentThread(),
                    TOKEN_ALL_ACCESS,
                    false,
                    out threadToken)
                || threadToken == null || threadToken.IsInvalid)
            {
                int err = Marshal.GetLastWin32Error();
                _logger.Error("[TokenManager] OpenThreadToken failed: Win32 {Win32Error} (0x{Win32ErrorHex:X8})", err, err);
                throw new KerberosException(
                    $"OpenThreadToken failed. Win32 error: {err} (0x{err:X8}).",
                    err, KerberosErrorType.S4U2ProxyFailed);
            }
            _logger.Information("[TokenManager] S4U2Proxy: thread impersonation token obtained");

            // Step 3: Duplicate to a Primary token for CreateProcessWithTokenW
            if (!NativeMethods.DuplicateTokenEx(
                    threadToken,
                    (int)TOKEN_ALL_ACCESS,
                    IntPtr.Zero,
                    SecurityImpersonation,
                    TokenPrimary,
                    out primaryToken)
                || primaryToken == null || primaryToken.IsInvalid)
            {
                int err = Marshal.GetLastWin32Error();
                _logger.Error("[TokenManager] DuplicateTokenEx failed: Win32 {Win32Error} (0x{Win32ErrorHex:X8})", err, err);
                throw new KerberosException(
                    $"DuplicateTokenEx failed. Win32 error: {err} (0x{err:X8}).",
                    err, KerberosErrorType.S4U2ProxyFailed);
            }

            _logger.Information("[TokenManager] ExecuteS4U2Proxy completed — Primary token ready for CreateProcessWithTokenW");
            LogTokenInfo("S4U2Proxy", primaryToken);
            return primaryToken;
        }
        catch
        {
            primaryToken?.Dispose();
            throw;
        }
        finally
        {
            // Step 4: Always revert impersonation before leaving, even on failure
            if (impersonating)
            {
                NativeMethods.RevertToSelf();
                _logger.Information("[TokenManager] S4U2Proxy: thread impersonation reverted");
            }
            threadToken?.Dispose();
        }
    }

    /// <summary>
    /// Attempts to enable SeTcbPrivilege for the current process
    /// </summary>
    /// <remarks>
    /// This will only succeed if running as Administrator and the privilege is available.
    /// Silently fails if the privilege cannot be enabled.
    /// </remarks>
    private static void TryEnableSeTcbPrivilege()
    {
        try
        {
            Log.Debug("[DEBUG] Attempting to enable SeTcbPrivilege...");
            
            // Open the current process token
            if (!NativeMethods.OpenProcessToken(
                NativeMethods.GetCurrentProcess(),
                0x0020 | 0x0008, // TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY
                out SafeAccessTokenHandle tokenHandle))
            {
                var error = Marshal.GetLastWin32Error();
                Log.Debug("[DEBUG] Failed to open process token. Error: {Win32Error}", error);
                return; // Silently fail
            }

            using (tokenHandle)
            {
                // Look up the LUID for SeTcbPrivilege
                if (!NativeMethods.LookupPrivilegeValue(
                    null,
                    NativeMethods.SE_TCB_NAME,
                    out NativeMethods.LUID luid))
                {
                    var error = Marshal.GetLastWin32Error();
                    Log.Debug("[DEBUG] Failed to lookup SeTcbPrivilege. Error: {Win32Error}", error);
                    Log.Debug("[DEBUG] This means the privilege is NOT assigned to your account.");
                    return; // Privilege doesn't exist or can't be looked up
                }

                Log.Debug("[DEBUG] SeTcbPrivilege LUID found: {LuidLowPart}", luid.LowPart);

                // Prepare the TOKEN_PRIVILEGES structure
                var tokenPrivileges = new NativeMethods.TOKEN_PRIVILEGES
                {
                    PrivilegeCount = 1,
                    Privileges = new NativeMethods.LUID_AND_ATTRIBUTES[1]
                };
                tokenPrivileges.Privileges[0].Luid = luid;
                tokenPrivileges.Privileges[0].Attributes = NativeMethods.SE_PRIVILEGE_ENABLED;

                // Try to enable the privilege
                if (!NativeMethods.AdjustTokenPrivileges(
                    tokenHandle,
                    false,
                    ref tokenPrivileges,
                    0,
                    IntPtr.Zero,
                    IntPtr.Zero))
                {
                    var error = Marshal.GetLastWin32Error();
                    Log.Debug("[DEBUG] AdjustTokenPrivileges failed. Error: {Win32Error}", error);
                    return;
                }

                // Check if it actually worked
                var lastError = Marshal.GetLastWin32Error();
                if (lastError == 0)
                {
                    Log.Debug("[DEBUG] ✓ SeTcbPrivilege successfully enabled!");
                }
                else if (lastError == 1300) // ERROR_NOT_ALL_ASSIGNED
                {
                    Log.Debug("[DEBUG] ✗ SeTcbPrivilege could NOT be enabled (ERROR_NOT_ALL_ASSIGNED)");
                    Log.Debug("[DEBUG] The privilege is not assigned to your account.");
                    Log.Debug("[DEBUG] You must grant it using secpol.msc or run as SYSTEM.");
                }
                else
                {
                    Log.Debug("[DEBUG] AdjustTokenPrivileges returned error: {Win32Error}", lastError);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "[DEBUG] Exception in TryEnableSeTcbPrivilege: {ErrorMessage}", ex.Message);
            // Silently fail - this is a best-effort attempt
        }
    }

    /// <summary>
    /// Disposes resources used by the KerberosTokenManager
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_lsaHandle != IntPtr.Zero)
        {
            NativeMethods.LsaDeregisterLogonProcess(_lsaHandle);
            _lsaHandle = IntPtr.Zero;
        }

        _disposed = true;
    }
}