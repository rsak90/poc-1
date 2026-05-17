    /// <summary>
    /// Executes S4U2Self (Service for User to Self) to obtain a forwardable ticket for the target user
    /// </summary>
    /// <param name="serviceToken">Service account token</param>
    /// <param name="username">Username to impersonate (UPN or DOMAIN\username format)</param>
    /// <returns>SafeAccessTokenHandle representing the user's forwardable ticket</returns>
    /// <exception cref="KerberosException">Thrown when S4U2Self fails</exception>
    private SafeAccessTokenHandle ExecuteS4U2Self(SafeAccessTokenHandle serviceToken, string username)
    {
        Serilog.Log.Information("[S4U2Self] Starting S4U2Self for username: {Username}", username);
        Console.WriteLine($"[TokenManager] ExecuteS4U2Self called for username: {username}");
        
        if (serviceToken == null || serviceToken.IsInvalid)
        {
            throw new ArgumentException("Invalid service token handle", nameof(serviceToken));
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username cannot be null or empty", nameof(username));
        }

        // Step 1: Parse username into domain and account components
        var (domain, account) = ParseUsername(username);
        Serilog.Log.Information("[S4U2Self] Parsed username - Domain: {Domain}, Account: {Account}", domain, account);
        Console.WriteLine($"[TokenManager] Parsed username - Domain: {domain}, Account: {account}");

        // Step 2: Register LSA authentication package
        Serilog.Log.Information("[S4U2Self] Registering LSA authentication package");
        Console.WriteLine($"[TokenManager] Registering LSA authentication package...");
        var authPackage = RegisterLsaAuthenticationPackage("Negotiate");

        // Validate that we got a valid authentication package ID
        if (authPackage == 0)
        {
            Serilog.Log.Error("[S4U2Self] Authentication package ID is 0");
            Console.WriteLine($"[TokenManager] ERROR: Authentication package ID is 0");
            throw new KerberosException(
                "Failed to obtain valid authentication package ID. LsaLookupAuthenticationPackage returned 0.",
                0,
                KerberosErrorType.S4U2SelfFailed);
        }
        Serilog.Log.Information("[S4U2Self] Authentication package ID: {PackageId}", authPackage);
        Console.WriteLine($"[TokenManager] Authentication package ID: {authPackage}");

        // Step 3: Build S4U_LOGON structure with username and domain information
        Serilog.Log.Information("[S4U2Self] Building S4U_LOGON structure");
        Console.WriteLine($"[TokenManager] Building S4U_LOGON structure...");
        
        IntPtr upnBuffer = IntPtr.Zero;
        IntPtr domainBuffer = IntPtr.Zero;
        IntPtr s4uLogonPtr = IntPtr.Zero;
        
        try
        {
            // Allocate and populate UPN buffer
            upnBuffer = Marshal.StringToHGlobalUni(username);
            var upnBytes = System.Text.Encoding.Unicode.GetByteCount(username);
            
            // Allocate and populate domain buffer
            domainBuffer = Marshal.StringToHGlobalUni(domain);
            var domainBytes = System.Text.Encoding.Unicode.GetByteCount(domain);
            
            Serilog.Log.Debug("[S4U2Self] UPN buffer allocated: {UpnBytes} bytes, Domain buffer: {DomainBytes} bytes", upnBytes, domainBytes);
            
            // Create UNICODE_STRING structures with valid pointers
            var upnString = new NativeMethods.UNICODE_STRING
            {
                Length = (ushort)upnBytes,
                MaximumLength = (ushort)(upnBytes + 2),
                Buffer = upnBuffer
            };
            
            var domainString = new NativeMethods.UNICODE_STRING
            {
                Length = (ushort)domainBytes,
                MaximumLength = (ushort)(domainBytes + 2),
                Buffer = domainBuffer
            };
            
            var s4uLogon = new NativeMethods.S4U_LOGON
            {
                MessageType = NativeMethods.MsV1_0S4ULogon,
                Flags = 0,
                UserPrincipalName = upnString,
                DomainName = domainString
            };

            // Marshal the S4U_LOGON structure to unmanaged memory
            var s4uLogonSize = Marshal.SizeOf<NativeMethods.S4U_LOGON>();
            s4uLogonPtr = Marshal.AllocHGlobal(s4uLogonSize);
            
            Marshal.StructureToPtr(s4uLogon, s4uLogonPtr, false);
            Serilog.Log.Debug("[S4U2Self] S4U_LOGON structure marshalled, size: {Size} bytes", s4uLogonSize);

            // Step 4: Prepare origin name and token source
            var originName = NativeMethods.CreateLsaString("S4U2Self");
            try
            {
                // TOKEN_SOURCE.SourceName must be exactly 8 bytes
                var sourceNameBytes = new byte[8];
                var sourceNameString = "S4U2Self";
                var encodedBytes = System.Text.Encoding.ASCII.GetBytes(sourceNameString);
                Array.Copy(encodedBytes, sourceNameBytes, Math.Min(encodedBytes.Length, 8));
                
                var tokenSource = new NativeMethods.TOKEN_SOURCE
                {
                    SourceName = sourceNameBytes,
                    SourceIdentifier = new NativeMethods.LUID { LowPart = 0, HighPart = 0 }
                };

                // Step 5: Call LsaLogonUser API with S4U2Self request
                Serilog.Log.Information("[S4U2Self] Calling LsaLogonUser with Network logon type");
                Console.WriteLine($"[TokenManager] Calling LsaLogonUser for S4U2Self...");
                var status = NativeMethods.LsaLogonUser(
                    _lsaHandle,
                    ref originName,
                    NativeMethods.Network,  // Use Security Logon Type, not LOGON32_*
                    authPackage,
                    s4uLogonPtr,
                    (uint)s4uLogonSize,
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
                    Serilog.Log.Error("[S4U2Self] LsaLogonUser failed - NTSTATUS: 0x{Status:X8}, SubStatus: 0x{SubStatus:X8}, Win32: {Win32Error}", 
                        status, subStatus, win32Error);
                    Console.WriteLine($"[TokenManager] ERROR: LsaLogonUser (S4U2Self) failed with status: 0x{status:X8}, SubStatus: 0x{subStatus:X8}, Win32 error: {win32Error}");
                    
                    // Check for specific error conditions
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

                Serilog.Log.Information("[S4U2Self] LsaLogonUser succeeded");
                Console.WriteLine($"[TokenManager] LsaLogonUser (S4U2Self) succeeded");

                // Validate token handle
                if (token == null || token.IsInvalid)
                {
                    Serilog.Log.Error("[S4U2Self] Returned token handle is invalid");
                    Console.WriteLine($"[TokenManager] ERROR: S4U2Self returned invalid token handle");
                    throw new KerberosException(
                        $"S4U2Self returned invalid token handle for user: {username}",
                        0,
                        KerberosErrorType.S4U2SelfFailed);
                }

                // Step 6: Validate returned token is forwardable (required for S4U2Proxy)
                Serilog.Log.Information("[S4U2Self] Checking if token is forwardable");
                Console.WriteLine($"[TokenManager] Checking if token is forwardable...");
                if (!IsTokenForwardable(token))
                {
                    token.Dispose();
                    Serilog.Log.Error("[S4U2Self] Token is not forwardable");
                    Console.WriteLine($"[TokenManager] ERROR: Token is not forwardable");
                    throw new KerberosException(
                        $"S4U2Self token is not forwardable for user: {username}. Token cannot be used for delegation.",
                        0,
                        KerberosErrorType.S4U2SelfFailed);
                }

                Serilog.Log.Information("[S4U2Self] Token is forwardable - ExecuteS4U2Self completed successfully");
                Console.WriteLine($"[TokenManager] Token is forwardable - ExecuteS4U2Self completed successfully");
                return token;
            }
            finally
            {
                NativeMethods.FreeLsaString(ref originName);
            }
        }
        catch (KerberosException ex)
        {
            Serilog.Log.Error(ex, "[S4U2Self] KerberosException occurred");
            throw;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[S4U2Self] Unexpected exception occurred");
            throw new KerberosException(
                $"Unexpected error during S4U2Self for user {username}: {ex.Message}",
                Marshal.GetLastWin32Error(),
                KerberosErrorType.S4U2SelfFailed,
                ex);
        }
        finally
        {
            // Clean up UNICODE_STRING buffers
            if (s4uLogonPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(s4uLogonPtr);
            }
            if (upnBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(upnBuffer);
            }
            if (domainBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(domainBuffer);
            }
            Serilog.Log.Debug("[S4U2Self] Cleanup completed");
        }
    }

    /// <summary>
    /// Executes S4U2Proxy (Service for User to Proxy) to obtain a service ticket for the target service using delegated credentials
    /// </summary>
    /// <param name="userToken">User token obtained from S4U2Self (must be forwardable)</param>
    /// <param name="targetSpn">Target Service Principal Name for delegation</param>
    /// <returns>SafeAccessTokenHandle representing delegated credentials for target service</returns>
    /// <exception cref="KerberosException">Thrown when S4U2Proxy fails</exception>
    private SafeAccessTokenHandle ExecuteS4U2Proxy(SafeAccessTokenHandle userToken, string targetSpn)
    {
        Serilog.Log.Information("[S4U2Proxy] Starting S4U2Proxy for target SPN: {TargetSpn}", targetSpn);
        Console.WriteLine($"[TokenManager] ExecuteS4U2Proxy called for target SPN: {targetSpn}");
        
        if (userToken == null || userToken.IsInvalid)
        {
            throw new ArgumentException("Invalid user token handle", nameof(userToken));
        }

        if (string.IsNullOrWhiteSpace(targetSpn))
        {
            throw new ArgumentException("Target SPN cannot be null or empty", nameof(targetSpn));
        }

        // Step 1: Register LSA authentication package
        Serilog.Log.Information("[S4U2Proxy] Registering LSA authentication package");
        Console.WriteLine($"[TokenManager] Registering LSA authentication package...");
        var authPackage = RegisterLsaAuthenticationPackage("Negotiate");

        // Validate that we got a valid authentication package ID
        if (authPackage == 0)
        {
            throw new KerberosException(
                "Failed to obtain valid authentication package ID. LsaLookupAuthenticationPackage returned 0.",
                0,
                KerberosErrorType.S4U2ProxyFailed);
        }

        // Step 2: Get user information from the token
        Serilog.Log.Information("[S4U2Proxy] Extracting user information from token");
        Console.WriteLine($"[TokenManager] Extracting user information from token...");
        var tokenUsername = GetTokenUsername(userToken);
        var tokenDomain = GetTokenDomain(userToken);
        var userPrincipalName = $"{tokenUsername}@{tokenDomain}";
        Serilog.Log.Information("[S4U2Proxy] Token user: {Username}, Domain: {Domain}, UPN: {Upn}", tokenUsername, tokenDomain, userPrincipalName);
        Console.WriteLine($"[TokenManager] Token user: {tokenUsername}, Domain: {tokenDomain}, UPN: {userPrincipalName}");

        // Step 3: Build S4U_LOGON structure with user information from token
        Serilog.Log.Information("[S4U2Proxy] Building S4U_LOGON structure");
        Console.WriteLine($"[TokenManager] Building S4U_LOGON structure...");
        
        IntPtr upnBuffer = IntPtr.Zero;
        IntPtr domainBuffer = IntPtr.Zero;
        IntPtr s4uLogonPtr = IntPtr.Zero;
        IntPtr targetSpnBuffer = IntPtr.Zero;
        IntPtr additionalInfoPtr = IntPtr.Zero;
        
        try
        {
            // Allocate and populate UPN buffer
            upnBuffer = Marshal.StringToHGlobalUni(userPrincipalName);
            var upnBytes = System.Text.Encoding.Unicode.GetByteCount(userPrincipalName);
            
            // Allocate and populate domain buffer
            domainBuffer = Marshal.StringToHGlobalUni(tokenDomain);
            var domainBytes = System.Text.Encoding.Unicode.GetByteCount(tokenDomain);
            
            Serilog.Log.Debug("[S4U2Proxy] UPN buffer: {UpnBytes} bytes, Domain buffer: {DomainBytes} bytes", upnBytes, domainBytes);
            
            // Create UNICODE_STRING structures with valid pointers
            var upnString = new NativeMethods.UNICODE_STRING
            {
                Length = (ushort)upnBytes,
                MaximumLength = (ushort)(upnBytes + 2),
                Buffer = upnBuffer
            };
            
            var domainString = new NativeMethods.UNICODE_STRING
            {
                Length = (ushort)domainBytes,
                MaximumLength = (ushort)(domainBytes + 2),
                Buffer = domainBuffer
            };
            
            var s4uLogon = new NativeMethods.S4U_LOGON
            {
                MessageType = NativeMethods.MsV1_0S4ULogon,
                Flags = NativeMethods.S4U_LOGON_FLAG_IDENTITY,
                UserPrincipalName = upnString,
                DomainName = domainString
            };

            // Marshal the S4U_LOGON structure to unmanaged memory
            var s4uLogonSize = Marshal.SizeOf<NativeMethods.S4U_LOGON>();
            s4uLogonPtr = Marshal.AllocHGlobal(s4uLogonSize);
            
            Marshal.StructureToPtr(s4uLogon, s4uLogonPtr, false);

            // Step 4: Build KERB_S4U_LOGON_ADDITIONAL_INFO structure with target SPN
            Serilog.Log.Information("[S4U2Proxy] Building KERB_S4U_LOGON_ADDITIONAL_INFO with target SPN");
            
            // Allocate and populate target SPN buffer
            targetSpnBuffer = Marshal.StringToHGlobalUni(targetSpn);
            var targetSpnBytes = System.Text.Encoding.Unicode.GetByteCount(targetSpn);
            
            var targetSpnString = new NativeMethods.UNICODE_STRING
            {
                Length = (ushort)targetSpnBytes,
                MaximumLength = (ushort)(targetSpnBytes + 2),
                Buffer = targetSpnBuffer
            };
            
            var additionalInfo = new NativeMethods.KERB_S4U_LOGON_ADDITIONAL_INFO
            {
                TargetServerName = targetSpnString
            };

            // Marshal the additional info structure
            var additionalInfoSize = Marshal.SizeOf<NativeMethods.KERB_S4U_LOGON_ADDITIONAL_INFO>();
            additionalInfoPtr = Marshal.AllocHGlobal(additionalInfoSize);
            
            Marshal.StructureToPtr(additionalInfo, additionalInfoPtr, false);

            // Step 5: Prepare origin name and token source
            var originName = NativeMethods.CreateLsaString("S4U2Proxy");
            try
            {
                // TOKEN_SOURCE.SourceName must be exactly 8 bytes
                var sourceNameBytes = new byte[8];
                var sourceNameString = "S4U2Prxy"; // Exactly 8 characters
                var encodedBytes = System.Text.Encoding.ASCII.GetBytes(sourceNameString);
                Array.Copy(encodedBytes, sourceNameBytes, Math.Min(encodedBytes.Length, 8));
                
                var tokenSource = new NativeMethods.TOKEN_SOURCE
                {
                    SourceName = sourceNameBytes,
                    SourceIdentifier = new NativeMethods.LUID { LowPart = 0, HighPart = 0 }
                };

                // Step 6: Call LsaLogonUser API with S4U2Proxy request
                Serilog.Log.Information("[S4U2Proxy] Calling LsaLogonUser with NetworkCleartext logon type");
                Console.WriteLine($"[TokenManager] Calling LsaLogonUser for S4U2Proxy...");
                var status = NativeMethods.LsaLogonUser(
                    _lsaHandle,
                    ref originName,
                    NativeMethods.NetworkCleartext,  // Use Security Logon Type, not LOGON32_*
                    authPackage,
                    s4uLogonPtr,
                    (uint)s4uLogonSize,
                    additionalInfoPtr,
                    ref tokenSource,
                    out IntPtr profileBuffer,
                    out uint profileBufferLength,
                    out NativeMethods.LUID logonId,
                    out SafeAccessTokenHandle delegatedToken,
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
                    Serilog.Log.Error("[S4U2Proxy] LsaLogonUser failed - NTSTATUS: 0x{Status:X8}, SubStatus: 0x{SubStatus:X8}, Win32: {Win32Error}", 
                        status, subStatus, win32Error);
                    Console.WriteLine($"[TokenManager] ERROR: LsaLogonUser (S4U2Proxy) failed with status: 0x{status:X8}, SubStatus: 0x{subStatus:X8}, Win32 error: {win32Error}");
                    
                    // Step 7: Handle ERROR_NOT_SUPPORTED error (service account not trusted for delegation)
                    if (win32Error == NativeMethods.ERROR_NOT_SUPPORTED)
                    {
                        throw new KerberosException(
                            $"Service account is not trusted for delegation to target SPN: {targetSpn}. " +
                            $"Please configure constrained delegation in Active Directory. NTSTATUS: 0x{status:X8}",
                            win32Error,
                            KerberosErrorType.DelegationNotConfigured);
                    }

                    // Step 8: Throw KerberosException with ErrorType.S4U2ProxyFailed on other failures
                    throw new KerberosException(
                        $"LsaLogonUser (S4U2Proxy) failed for target SPN: {targetSpn}. NTSTATUS: 0x{status:X8}, SubStatus: 0x{subStatus:X8}",
                        win32Error,
                        KerberosErrorType.S4U2ProxyFailed);
                }

                Serilog.Log.Information("[S4U2Proxy] LsaLogonUser succeeded");
                Console.WriteLine($"[TokenManager] LsaLogonUser (S4U2Proxy) succeeded");

                // Validate token handle
                if (delegatedToken == null || delegatedToken.IsInvalid)
                {
                    Serilog.Log.Error("[S4U2Proxy] Returned token handle is invalid");
                    Console.WriteLine($"[TokenManager] ERROR: S4U2Proxy returned invalid token handle");
                    throw new KerberosException(
                        $"S4U2Proxy returned invalid token handle for target SPN: {targetSpn}",
                        0,
                        KerberosErrorType.S4U2ProxyFailed);
                }

                // Step 9: Validate delegated token username matches original user identity
                Serilog.Log.Information("[S4U2Proxy] Validating delegated token username");
                Console.WriteLine($"[TokenManager] Validating delegated token username...");
                var delegatedUsername = GetTokenUsername(delegatedToken);
                var delegatedDomain = GetTokenDomain(delegatedToken);
                var fullDelegatedUsername = $"{delegatedDomain}\\{delegatedUsername}";
                var fullOriginalUsername = $"{tokenDomain}\\{tokenUsername}";
                Serilog.Log.Information("[S4U2Proxy] Delegated token user: {DelegatedUser}, Original user: {OriginalUser}", 
                    fullDelegatedUsername, fullOriginalUsername);
                Console.WriteLine($"[TokenManager] Delegated token user: {fullDelegatedUsername}, Original user: {fullOriginalUsername}");

                if (!string.Equals(delegatedUsername, tokenUsername, StringComparison.OrdinalIgnoreCase))
                {
                    delegatedToken.Dispose();
                    Serilog.Log.Error("[S4U2Proxy] Delegated token username mismatch");
                    Console.WriteLine($"[TokenManager] ERROR: Delegated token username mismatch");
                    throw new KerberosException(
                        $"Delegated token username mismatch. Expected: {fullOriginalUsername}, Got: {fullDelegatedUsername}",
                        0,
                        KerberosErrorType.S4U2ProxyFailed);
                }

                // Step 10: Return SafeAccessTokenHandle representing delegated credentials for target service
                Serilog.Log.Information("[S4U2Proxy] ExecuteS4U2Proxy completed successfully");
                Console.WriteLine($"[TokenManager] ExecuteS4U2Proxy completed successfully");
                return delegatedToken;
            }
            finally
            {
                NativeMethods.FreeLsaString(ref originName);
            }
        }
        catch (KerberosException ex)
        {
            Serilog.Log.Error(ex, "[S4U2Proxy] KerberosException occurred");
            throw;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "[S4U2Proxy] Unexpected exception occurred");
            throw new KerberosException(
                $"Unexpected error during S4U2Proxy for target SPN {targetSpn}: {ex.Message}",
                Marshal.GetLastWin32Error(),
                KerberosErrorType.S4U2ProxyFailed,
                ex);
        }
        finally
        {
            // Clean up all allocated buffers
            if (additionalInfoPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(additionalInfoPtr);
            }
            if (targetSpnBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(targetSpnBuffer);
            }
            if (s4uLogonPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(s4uLogonPtr);
            }
            if (upnBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(upnBuffer);
            }
            if (domainBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(domainBuffer);
            }
            Serilog.Log.Debug("[S4U2Proxy] Cleanup completed");
        }
    }
