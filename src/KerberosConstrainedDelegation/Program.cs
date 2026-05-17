using Microsoft.Extensions.Configuration;
using Serilog;

namespace KerberosConstrainedDelegation;

/// <summary>
/// Main entry point for the Kerberos Constrained Delegation application
/// </summary>
public class Program
{
    public static int Main(string[] args)
    {
        // Get the directory where the executable is located (important for Windows services)
        var baseDirectory = AppContext.BaseDirectory;

        // Load configuration to get log file path
        var configuration = new ConfigurationBuilder()
            .SetBasePath(baseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Get log file path from configuration
        var logFilePath = configuration["Serilog:WriteTo:0:Args:path"] ?? Path.Combine(baseDirectory, "logs", "app-.log");

        // Initialize Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        try
        {
            Log.Information("Kerberos Constrained Delegation Application");
            Log.Information("============================================");
            Log.Information("");

            ConfigurationManager? configManager = null;
            KerberosTokenManager? tokenManager = null;

            try
            {
            // Step 1: Load configuration from appsettings.json or command-line arguments
            Log.Information("Loading configuration...");
            configManager = ConfigurationManager.CreateFromArgs(args);

            // Step 2: Validate configuration
            Log.Information("Validating configuration...");
            var validationResult = configManager.ValidateConfiguration();

            if (!validationResult.IsValid)
            {
                Log.Error("Configuration validation failed");
                Log.Error("Details: {ErrorMessage}", validationResult.ErrorMessage);
                Log.Error("Please check your appsettings.json file or command-line arguments.");
                return 1; // Exit code 1 for configuration validation failure
            }

            Log.Information("Configuration validated successfully");

            // Step 3: Get configuration values
            var serviceCredentials = configManager.GetServiceCredentials();
            var targetUsername = configManager.GetTargetUsername();
            var targetSpn = configManager.GetTargetServicePrincipalName();
            var executablePath = configManager.GetExternalExecutablePath();
            var fileSharePath = configManager.GetFileSharePath();
            var timeoutSeconds = configManager.GetTimeoutSeconds();

            Log.Information("Service Account: {ServiceAccount}", serviceCredentials.FullyQualifiedUsername);
            Log.Information("Target User: {TargetUser}", targetUsername);
            Log.Information("Target SPN: {TargetSpn}", targetSpn);
            Log.Information("Executable: {Executable}", executablePath);
            Log.Information("File Share: {FileShare}", fileSharePath);
            Log.Information("Timeout: {Timeout} seconds", timeoutSeconds);
            Log.Information("");

            // Step 4: Initialize KerberosTokenManager with service account credentials
            Log.Information("Initializing Kerberos Token Manager...");
            tokenManager = new KerberosTokenManager(serviceCredentials);

            // Step 5: Validate delegation configuration
            Log.Information("Validating delegation configuration...");
            if (!tokenManager.ValidateDelegationConfiguration())
            {
                Log.Error("Service account is not properly configured for constrained delegation");
                Log.Error("TROUBLESHOOTING INSTRUCTIONS:");
                Log.Error("1. Verify the service account credentials are correct");
                Log.Error("2. Ensure the service account is configured for constrained delegation in Active Directory");
                Log.Error("3. Verify the target SPN is in the allowed delegation list for the service account");
                Log.Error("4. Check that the service account has 'Trust this user for delegation to specified services only' enabled");
                Log.Error("5. Use 'setspn -L <service_account>' to verify SPN configuration");
                Log.Error("6. Use 'klist' to view current Kerberos tickets");
                return 2; // Exit code 2 for delegation configuration failure
            }

            Log.Information("Delegation configuration validated successfully");
            Log.Information("");

            // Step 6: Obtain delegated token for target user and SPN
            Log.Information("Obtaining delegated token for user '{TargetUser}' and SPN '{TargetSpn}'...", targetUsername, targetSpn);
            
            using var delegatedToken = tokenManager.GetDelegatedToken(targetUsername, targetSpn);

            Log.Information("Successfully obtained delegated token");
            Log.Information("Delegation successful for user: {TargetUser}", targetUsername);
            Log.Information("Target SPN: {TargetSpn}", targetSpn);
            Log.Information("");

            // Step 7: Initialize ProcessSpawner
            Log.Information("Initializing Process Spawner...");
            var processSpawner = new ProcessSpawner();

            // Step 8: Build command-line arguments for FileShareWriter
            var arguments = $"\"{fileSharePath}\" \"Test content from delegated process - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\"";

            // Step 9: Spawn FileShareWriter process with delegated token
            Log.Information("Spawning process: {Executable}", executablePath);
            Log.Information("Arguments: {Arguments}", arguments);
            Log.Information("");

            var result = processSpawner.SpawnProcessWithToken(
                delegatedToken,
                executablePath,
                arguments,
                timeoutSeconds * 1000); // Convert seconds to milliseconds

            // Step 10: Display process execution results
            Log.Information("");
            Log.Information("Process Execution Results:");
            Log.Information("Exit Code: {ExitCode}", result.ExitCode);
            Log.Information("Execution Time: {ExecutionTime:F2} seconds", result.ExecutionTime.TotalSeconds);
            Log.Information("Timed Out: {TimedOut}", result.TimedOut);
            Log.Information("");

            if (!string.IsNullOrWhiteSpace(result.StandardOutput))
            {
                Log.Information("Standard Output:");
                Log.Information("----------------");
                Log.Information("{StandardOutput}", result.StandardOutput);
                Log.Information("");
            }

            if (!string.IsNullOrWhiteSpace(result.StandardError))
            {
                Log.Warning("Standard Error:");
                Log.Warning("---------------");
                Log.Warning("{StandardError}", result.StandardError);
                Log.Information("");
            }

            // Step 11: Return appropriate exit code
            if (result.IsSuccess)
            {
                Log.Information("SUCCESS: Delegation and file write completed successfully");
                return 0; // Exit code 0 for success
            }
            else
            {
                Log.Error("Process failed with exit code {ExitCode}", result.ExitCode);
                if (result.TimedOut)
                {
                    Log.Error("Process exceeded timeout of {Timeout} seconds", timeoutSeconds);
                }
                return 3; // Exit code 3 for process failure
            }
        }
        catch (KerberosException ex)
        {
            // Step 12: Handle KerberosException with detailed error display
            Console.WriteLine();
            Console.Error.WriteLine("KERBEROS ERROR:");
            Console.Error.WriteLine("===============");
            Console.Error.WriteLine($"Message: {ex.Message}");
            Console.Error.WriteLine($"Error Type: {ex.ErrorType}");
            Console.Error.WriteLine($"Win32 Error Code: 0x{ex.Win32ErrorCode:X8} ({ex.Win32ErrorCode})");
            Console.Error.WriteLine();

            // Provide troubleshooting guidance based on error type
            Console.Error.WriteLine("TROUBLESHOOTING GUIDANCE:");
            Console.Error.WriteLine("-------------------------");

            switch (ex.ErrorType)
            {
                case KerberosErrorType.ServiceAuthenticationFailed:
                    Console.Error.WriteLine("- Verify service account credentials (username, domain, password)");
                    Console.Error.WriteLine("- Check if the service account is enabled in Active Directory");
                    Console.Error.WriteLine("- Ensure the service account password has not expired");
                    Console.Error.WriteLine("- Verify network connectivity to Domain Controllers");
                    break;

                case KerberosErrorType.UserNotFound:
                    Console.Error.WriteLine("- Verify the target username exists in Active Directory");
                    Console.Error.WriteLine("- Check the username format (UPN: user@domain.com or DOMAIN\\username)");
                    Console.Error.WriteLine("- Ensure the user account is enabled");
                    break;

                case KerberosErrorType.DelegationNotConfigured:
                    Console.Error.WriteLine("- Configure constrained delegation for the service account in Active Directory");
                    Console.Error.WriteLine("- Add the target SPN to the allowed delegation list");
                    Console.Error.WriteLine("- Enable 'Trust this user for delegation to specified services only'");
                    Console.Error.WriteLine("- Use 'setspn -L <service_account>' to verify SPN configuration");
                    break;

                case KerberosErrorType.S4U2SelfFailed:
                    Console.Error.WriteLine("- Verify the service account has permission to request tickets on behalf of users");
                    Console.Error.WriteLine("- Check if the target user is marked as 'Account is sensitive and cannot be delegated'");
                    Console.Error.WriteLine("- Ensure time synchronization between client and Domain Controller (within 5 minutes)");
                    break;

                case KerberosErrorType.S4U2ProxyFailed:
                    Console.Error.WriteLine("- Verify the service account is trusted for delegation to the target SPN");
                    Console.Error.WriteLine("- Check if the target SPN is registered in Active Directory");
                    Console.Error.WriteLine("- Use 'setspn -Q <spn>' to verify SPN registration");
                    Console.Error.WriteLine("- Ensure the target service supports Kerberos authentication");
                    break;

                case KerberosErrorType.TokenCreationFailed:
                    Console.Error.WriteLine("- Check Windows Event Logs for security audit failures");
                    Console.Error.WriteLine("- Verify the application has sufficient privileges");
                    Console.Error.WriteLine("- Ensure Kerberos encryption types are properly configured");
                    break;

                case KerberosErrorType.ProcessSpawnFailed:
                    Console.Error.WriteLine("- Verify the external executable path is correct");
                    Console.Error.WriteLine("- Check if the executable has proper permissions");
                    Console.Error.WriteLine("- Ensure the delegated token is valid");
                    break;

                default:
                    Console.Error.WriteLine("- Check Windows Event Logs for more details");
                    Console.Error.WriteLine("- Verify network connectivity and DNS resolution");
                    Console.Error.WriteLine("- Use 'klist' to view current Kerberos tickets");
                    break;
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine("For more information, check:");
            Console.Error.WriteLine("- Windows Event Viewer (Security and System logs)");
            Console.Error.WriteLine("- Use 'klist' command to view Kerberos tickets");
            Console.Error.WriteLine("- Use 'setspn' command to verify SPN configuration");

            return 4; // Exit code 4 for Kerberos errors
        }
        catch (Exception ex)
        {
            // Step 13: Handle general exceptions with stack trace display
            Console.WriteLine();
            Console.Error.WriteLine("UNEXPECTED ERROR:");
            Console.Error.WriteLine("=================");
            Console.Error.WriteLine($"Message: {ex.Message}");
            Console.Error.WriteLine($"Type: {ex.GetType().FullName}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Stack Trace:");
            Console.Error.WriteLine(ex.StackTrace);

            if (ex.InnerException != null)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("Inner Exception:");
                Console.Error.WriteLine($"Message: {ex.InnerException.Message}");
                Console.Error.WriteLine($"Type: {ex.InnerException.GetType().FullName}");
            }

            return 5; // Exit code 5 for general exceptions
        }
        finally
        {
            // Clean up resources
            tokenManager?.Dispose();
            configManager?.Dispose();
        }
        }
        finally
        {
            // Close and flush Serilog
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Logs a message with a timestamp
    /// </summary>
    /// <param name="message">Message to log</param>
    private static void LogWithTimestamp(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        Console.WriteLine($"[{timestamp}] {message}");
    }
}
