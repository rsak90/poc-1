# Implementation Plan: Kerberos Constrained Delegation App

## Overview

This implementation plan consolidates the development of a C# console application that implements Kerberos constrained delegation using S4U2Self and S4U2Proxy extensions. The application authenticates as a service account, obtains delegated credentials for a target user, and spawns an external process that writes to a network file share to demonstrate successful double-hop authentication.

## Tasks

- [ ] 1. Set up project structure and Windows API infrastructure
  - Create C# console application solution with main project (KerberosConstrainedDelegation), FileShareWriter project, and test projects
  - Configure target framework (.NET 9.0) and add necessary NuGet packages
  - Create NativeMethods class with P/Invoke declarations for Windows Security APIs (LogonUser, LsaLogonUser, CreateProcessAsUser, CreatePipe, WaitForSingleObject, GetExitCodeProcess, GetTokenInformation, etc.)
  - Define native structures (STARTUPINFO, PROCESS_INFORMATION, S4U_LOGON, KERB_S4U_LOGON_ADDITIONAL_INFO, LUID, QUOTA_LIMITS, TOKEN_USER, SID_AND_ATTRIBUTES)
  - Define constants for logon types, process creation flags, NTSTATUS codes, Win32 error codes, S4U logon message types, and token information classes
  - _Requirements: 3.1.1, 3.1.5, 3.1.6, 5.1.1, 5.1.3_

- [ ] 2. Implement core data models and exception types
  - Create ServiceAccountCredentials class with Username, Domain, Password (SecureString) properties, FullyQualifiedUsername property, validation logic, and IDisposable implementation
  - Create ProcessExecutionResult class with ExitCode, StandardOutput, StandardError, TimedOut, ExecutionTime properties and IsSuccess computed property
  - Create UserIdentityInfo class with Username, Domain, AuthenticationType, IsAuthenticated, Sid properties and FullyQualifiedName computed property
  - Create KerberosException class with Win32ErrorCode and ErrorType properties, and define KerberosErrorType enum (ServiceAuthenticationFailed, UserNotFound, DelegationNotConfigured, S4U2SelfFailed, S4U2ProxyFailed, TokenCreationFailed, ProcessSpawnFailed)
  - Create ValidationResult class with IsValid, ErrorMessage properties and Success()/Failure() factory methods
  - _Requirements: 1.2, 1.8, 2.3.1, 2.3.2_

- [x] 3. Implement configuration management system
  - Define IConfigurationManager interface with methods for GetServiceCredentials(), GetTargetUsername(), GetTargetServicePrincipalName(), GetExternalExecutablePath(), GetFileSharePath(), ValidateConfiguration()
  - Create ConfigurationManager class implementing IConfigurationManager
  - Implement configuration loading from app.config and command-line arguments
  - Implement comprehensive validation for service account credentials format, username format (UPN or DOMAIN\username), SPN format (service/host.domain.com), executable path existence, and UNC path format
  - Create app.config template with all required settings (service account, target user, target SPN, executable path, file share path, timeout)
  - Use SecureString for password storage and handling
  - _Requirements: 1.7, 2.2.1, 2.4.2_

- [x] 4. Implement Kerberos token management helper methods
  - Define IKerberosTokenManager interface with GetDelegatedToken() and ValidateDelegationConfiguration() methods
  - Implement ParseUsername() to extract domain and account from UPN or DOMAIN\username formats
  - Implement ValidateToken() to verify token represents expected user identity
  - Implement GetTokenUsername() and GetTokenDomain() to extract identity information from tokens
  - Implement IsTokenForwardable() to check if token can be used for delegation
  - Implement RegisterLsaAuthenticationPackage() to register "Negotiate" authentication package
  - _Requirements: 1.8, 1.9_

- [x] 5. Implement service account authentication
  - Create AuthenticateServiceAccount() method in KerberosTokenManager
  - Call LogonUser API with service account credentials using LOGON32_LOGON_NETWORK logon type
  - Validate returned token handle is valid
  - Throw KerberosException with ErrorType.ServiceAuthenticationFailed on failure, including Win32 error code
  - Return SafeAccessTokenHandle for use in delegation operations
  - _Requirements: 1.2, 2.3.1_


- [x] 6. Implement S4U2Self (Service for User to Self) delegation
  - Create ExecuteS4U2Self() method accepting service token and username
  - Parse username into domain and account components using ParseUsername()
  - Register LSA authentication package ("Negotiate")
  - Build S4U_LOGON structure with username and domain information
  - Call LsaLogonUser API with S4U2Self request to obtain forwardable ticket for target user
  - Validate returned token is forwardable (required for S4U2Proxy)
  - Throw KerberosException with ErrorType.S4U2SelfFailed or ErrorType.UserNotFound on failure
  - Return SafeAccessTokenHandle representing user's forwardable ticket
  - _Requirements: 1.3, 2.3.1_

- [x] 7. Implement S4U2Proxy (Service for User to Proxy) delegation
  - Create ExecuteS4U2Proxy() method accepting user token and target SPN
  - Register LSA authentication package ("Negotiate")
  - Build S4U_LOGON structure with user information from token
  - Build KERB_S4U_LOGON_ADDITIONAL_INFO structure with target SPN
  - Call LsaLogonUser API with S4U2Proxy request to obtain service ticket for target SPN
  - Validate delegated token username matches original user identity
  - Handle ERROR_NOT_SUPPORTED error (service account not trusted for delegation)
  - Throw KerberosException with ErrorType.S4U2ProxyFailed or ErrorType.DelegationNotConfigured on failure
  - Return SafeAccessTokenHandle representing delegated credentials for target service
  - _Requirements: 1.4, 2.2.2, 2.3.1_

- [x] 8. Implement complete delegation workflow and validation
  - Create GetDelegatedToken() method orchestrating full delegation flow
  - Call AuthenticateServiceAccount() to obtain service token
  - Call ExecuteS4U2Self() with service token and target username to get user token
  - Call ExecuteS4U2Proxy() with user token and target SPN to get delegated token
  - Validate delegated token represents correct user identity
  - Dispose intermediate tokens (service token, user token) in finally block
  - Return delegated token to caller (caller responsible for disposal)
  - Implement ValidateDelegationConfiguration() to query Active Directory for service account delegation settings
  - Create KerberosTokenManager class implementing IKerberosTokenManager with proper error handling and resource cleanup
  - _Requirements: 1.1, 1.10, 2.1.3, 2.3.2_

- [x] 9. Implement process spawning with delegated credentials
  - Define IProcessSpawner interface with SpawnProcessWithToken() method
  - Create pipe management methods: CreatePipe() and ReadFromPipe() with asynchronous reading to prevent deadlocks
  - Implement SpawnProcessWithToken() method in ProcessSpawner class
  - Validate token handle and executable path
  - Create STARTUPINFO structure with pipe handles for stdout/stderr capture
  - Create pipes for standard output and error
  - Call CreateProcessAsUser API with delegated token to spawn process with user's credentials
  - Close write ends of pipes (child process owns them)
  - Wait for process completion with configurable timeout (default: 30 seconds)
  - Terminate process if timeout exceeded
  - Get process exit code and read standard output/error from pipes
  - Close all handles (process, thread, pipes) properly
  - Return ProcessExecutionResult with exit code, output, error, timeout status, and execution time
  - _Requirements: 1.5, 2.1.1, 2.1.2, 2.3.2, 2.3.3_

- [x] 10. Implement FileShareWriter external process
  - Create FileShareWriter console application project
  - Implement Main() method accepting UNC path and content as command-line arguments
  - Validate command-line arguments (require exactly 2 arguments)
  - Get current Windows identity using WindowsIdentity.GetCurrent()
  - Display current identity information (username, authentication type, IsAuthenticated, SID) to standard output
  - Validate UNC path format (must start with \\)
  - Check that target directory exists
  - Write content to file at UNC path, including timestamp and user identity in the content
  - Verify file was created successfully
  - Return exit code 0 on success
  - Handle UnauthorizedAccessException (return exit code 5), IOException (return exit code 6), and general exceptions (return exit code 7)
  - Log detailed error messages to standard error with troubleshooting guidance
  - _Requirements: 1.6, 2.4.1_

- [x] 11. Implement main application entry point and orchestration
  - Create Program class with Main() entry point
  - Load configuration using ConfigurationManager from app.config or command-line arguments
  - Validate configuration and display validation errors if invalid (exit code 1)
  - Initialize KerberosTokenManager with service account credentials
  - Validate delegation configuration and display instructions if not configured (exit code 2)
  - Obtain delegated token for target user and SPN using GetDelegatedToken()
  - Log delegation success with user and SPN information
  - Initialize ProcessSpawner
  - Build command-line arguments for FileShareWriter (UNC path and test content)
  - Spawn FileShareWriter process with delegated token
  - Display process execution results (exit code, execution time, stdout, stderr)
  - Return exit code 0 for success, 3 for process failure
  - Implement try-catch for KerberosException with detailed error display (message, error type, Win32 code) and troubleshooting guidance (exit code 4)
  - Implement try-catch for general exceptions with stack trace display (exit code 5)
  - Add console logging for all major operations with timestamps
  - _Requirements: 1.1, 2.3.1, 2.4.1, 2.4.3_

- [ ] 12. Implement security hardening measures
  - Ensure SecureString is used for all password storage in memory
  - Clear sensitive data from memory after use (dispose SecureString properly)
  - Verify passwords and token contents are never logged
  - Implement audit logging for all delegation attempts (success and failure) with user identity, target SPN, timestamp, and Win32 error codes
  - Ensure no sensitive data appears in audit logs
  - Implement comprehensive input validation for all user inputs
  - Sanitize executable paths to prevent path traversal attacks
  - Validate SPN format, username format, and UNC path format
  - Optionally add support for Windows Credential Manager or Azure Key Vault for credential storage
  - Optionally implement rate limiting for delegation operations with configurable threshold
  - Optionally implement executable signature verification before spawning processes
  - _Requirements: 2.2.1, 2.2.2, 2.2.3, 2.2.4, 2.2.5, 2.2.7, 3.2.1, 3.2.2, 3.2.3, 3.2.4, 3.2.5_

- [ ] 13. Implement performance optimizations
  - Implement thread-safe token caching for service account token (lifetime: 10 hours)
  - Implement token expiration handling and automatic renewal
  - Do NOT cache delegated tokens (security requirement - dispose immediately after use)
  - Add performance counters for token acquisition duration, process spawn duration, and end-to-end execution time
  - Log performance metrics for monitoring
  - Add alerts for slow operations (token acquisition > 5 seconds, process spawn > 3 seconds)
  - Optimize pipe buffer sizes for efficient I/O
  - Use asynchronous I/O where appropriate to prevent blocking
  - Minimize memory allocations and token lifetime
  - _Requirements: 2.1.1, 2.1.2, 2.1.3, 2.1.4_

- [ ]* 14. Write unit tests for all components
  - Test ServiceAccountCredentials: valid creation, validation for null/empty fields, invalid characters, FullyQualifiedUsername property, IDisposable implementation
  - Test ProcessExecutionResult: IsSuccess property with various exit codes and timeout states
  - Test UserIdentityInfo: FullyQualifiedName property and all property assignments
  - Test KerberosException: exception creation, message, Win32ErrorCode, ErrorType properties
  - Test ConfigurationManager: loading from app.config and command-line, ValidateConfiguration with valid/invalid configurations (missing credentials, invalid formats, non-existent paths)
  - Test ParseUsername: UPN format, DOMAIN\username format, invalid formats, null/empty input
  - Test KerberosTokenManager with mocked Windows APIs: AuthenticateServiceAccount, ExecuteS4U2Self, ExecuteS4U2Proxy, GetDelegatedToken end-to-end, resource cleanup
  - Test ProcessSpawner with mocked CreateProcessAsUser: valid inputs, invalid token, non-existent executable, timeout handling, output/error capture, exit code retrieval, handle cleanup
  - Test FileShareWriter: valid arguments, missing arguments, invalid UNC path, file writing, identity retrieval, error handling
  - _Requirements: 2.6.1, 2.6.2_

- [ ]* 15. Set up integration test environment and perform end-to-end testing
  - Create or configure test Active Directory domain
  - Create test service account and configure for constrained delegation
  - Create test user accounts for impersonation
  - Register test SPNs in Active Directory
  - Create test file share with appropriate permissions
  - Document test environment setup steps
  - Test complete delegation flow with real Windows APIs (authenticate, S4U2Self, S4U2Proxy, spawn process, verify file write)
  - Test delegation configuration validation (properly configured, not configured, wrong SPN)
  - Test multiple user delegation (different users, verify correct identities, verify independence)
  - Test concurrent delegation operations (verify no resource contention or deadlocks)
  - Test error scenarios (invalid credentials, non-existent user, inaccessible file share, verify error messages)
  - _Requirements: 2.1.4, 2.3.1, 4.1.2, 4.1.3, 4.1.4, 4.2.1, 4.2.2, 4.2.3, 4.3.1, 4.3.2, 4.3.3, 4.3.4_

- [x]* 16. Create comprehensive documentation
  - Create README with overview, purpose, use cases, prerequisites (Windows, Active Directory, .NET), build instructions, configuration instructions, usage examples, and troubleshooting section
  - Create setup guide documenting Active Directory configuration, service account creation, constrained delegation configuration, SPN registration steps, file share setup with screenshots/diagrams
  - Create troubleshooting guide with common error scenarios, solutions, diagnostic commands (setspn, klist), delegation configuration verification, SPN registration verification
  - Add XML documentation comments to all public classes, methods, and properties with parameter descriptions, return value descriptions, and exception documentation
  - Create configuration examples with example app.config file, command-line usage examples, and scenarios for different use cases
  - _Requirements: 2.4.1, 2.4.2, 2.6.3_

- [ ]* 17. Perform final testing and validation
  - Perform end-to-end testing in production-like environment with multiple users, multiple SPNs, error scenarios, and logging verification
  - Perform security testing: verify credential protection, verify no sensitive data logged, verify constrained delegation (not unconstrained), verify input validation, optionally perform penetration testing
  - Perform performance testing: measure token acquisition time, process spawn time, end-to-end execution time, test with concurrent operations, verify performance meets requirements (token < 2s, spawn < 1s)
  - Perform stress testing: test with high load (many concurrent operations), monitor resource usage (CPU, memory, handles), verify no resource leaks, verify application stability
  - Conduct code review: review code quality, error handling, resource cleanup, security measures, documentation completeness
  - _Requirements: 2.1.1, 2.1.2, 2.1.3, 2.1.4, 2.2.1, 2.2.2, 2.2.3, 2.2.4, 2.2.5, 2.3.1, 2.3.2, 2.6.1_

- [ ] 18. Create deployment package and deploy
  - Build release configuration for all projects
  - Include main console application executable (KerberosConstrainedDelegation.exe)
  - Include FileShareWriter executable
  - Include configuration file template (app.config)
  - Include README and all documentation
  - Optionally create installer or deployment script
  - Create deployment guide with deployment steps, configuration steps, verification steps, and rollback procedures
  - Deploy to test environment and verify all components present, configuration works, application runs successfully, and file share access works
  - _Requirements: 2.5.1, 2.5.2, 2.5.3, 3.1.1, 3.1.2, 3.1.3, 3.1.4, 3.1.5, 3.1.6, 3.3.1, 3.3.2, 3.3.3, 3.3.4, 3.3.5_

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1"] },
    { "id": 1, "tasks": ["2", "3"] },
    { "id": 2, "tasks": ["4"] },
    { "id": 3, "tasks": ["5", "6"] },
    { "id": 4, "tasks": ["7"] },
    { "id": 5, "tasks": ["8", "10"] },
    { "id": 6, "tasks": ["9"] },
    { "id": 7, "tasks": ["11"] },
    { "id": 8, "tasks": ["12", "13"] },
    { "id": 9, "tasks": ["14", "15"] },
    { "id": 10, "tasks": ["16"] },
    { "id": 11, "tasks": ["17"] },
    { "id": 12, "tasks": ["18"] }
  ]
}
```

## Notes

- This is a Windows-specific implementation requiring Active Directory domain membership
- Service account must be configured for constrained delegation in Active Directory before testing
- Integration tests require a test Active Directory environment with proper delegation configuration
- Security is critical - ensure credentials are never logged and tokens are properly disposed
- The implementation uses P/Invoke extensively for Windows Security APIs
- Target framework is .NET 9.0 as specified in requirements
- Tasks marked with `*` are optional and can be skipped for faster MVP delivery
- Each task consolidates multiple detailed sub-tasks from the original 14 phases into high-level implementation milestones
- All tasks reference specific requirements from requirements.md for traceability
