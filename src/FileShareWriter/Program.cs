using System.Runtime.Versioning;
using System.Security.Principal;

namespace FileShareWriter;

/// <summary>
/// Standalone executable that writes to a network file share to demonstrate successful double-hop authentication
/// </summary>
[SupportedOSPlatform("windows")]
public class Program
{
    // Exit codes
    private const int EXIT_SUCCESS = 0;
    private const int EXIT_INVALID_ARGUMENTS = 1;
    private const int EXIT_INVALID_UNC_PATH = 2;
    private const int EXIT_DIRECTORY_NOT_FOUND = 3;
    private const int EXIT_IDENTITY_ERROR = 4;
    private const int EXIT_UNAUTHORIZED = 5;
    private const int EXIT_IO_ERROR = 6;
    private const int EXIT_GENERAL_ERROR = 7;

    public static int Main(string[] args)
    {
        try
        {
            // Validate command-line arguments (require exactly 2 arguments)
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Error: Invalid number of arguments.");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Usage: FileShareWriter.exe <UNC_PATH> <CONTENT>");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Arguments:");
                Console.Error.WriteLine("  UNC_PATH  - Network path in UNC format (e.g., \\\\server\\share\\file.txt)");
                Console.Error.WriteLine("  CONTENT   - Content to write to the file");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Troubleshooting:");
                Console.Error.WriteLine("  - Ensure you provide exactly 2 arguments");
                Console.Error.WriteLine("  - Use quotes around arguments containing spaces");
                return EXIT_INVALID_ARGUMENTS;
            }

            string uncPath = args[0];
            string content = args[1];

            // Get current Windows identity
            WindowsIdentity? identity = null;
            try
            {
                identity = WindowsIdentity.GetCurrent();
                
                if (identity == null)
                {
                    Console.Error.WriteLine("Error: Failed to retrieve current Windows identity.");
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("Troubleshooting:");
                    Console.Error.WriteLine("  - Ensure the process is running with valid Windows credentials");
                    Console.Error.WriteLine("  - Check if the process was spawned correctly with CreateProcessAsUser");
                    return EXIT_IDENTITY_ERROR;
                }

                // Display current identity information
                Console.WriteLine("Current Process Identity:");
                Console.WriteLine($"  Username: {identity.Name}");
                Console.WriteLine($"  Authentication Type: {identity.AuthenticationType}");
                Console.WriteLine($"  Is Authenticated: {identity.IsAuthenticated}");
                Console.WriteLine($"  SID: {identity.User}");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: Failed to retrieve Windows identity: {ex.Message}");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Troubleshooting:");
                Console.Error.WriteLine("  - Ensure the process has permission to query its own identity");
                Console.Error.WriteLine("  - Check if running in a valid Windows security context");
                return EXIT_IDENTITY_ERROR;
            }

            // Accept both local paths and UNC paths.
            // A local path write confirms the spawned process token is valid.
            // A UNC path write confirms the full Kerberos delegation chain works.
            bool isUncPath = uncPath.StartsWith(@"\\");

            // For UNC paths, verify the target directory is reachable
            if (isUncPath)
            {
                string? directory = Path.GetDirectoryName(uncPath);
                if (string.IsNullOrEmpty(directory))
                {
                    Console.Error.WriteLine($"Error: Could not determine directory from UNC path: {uncPath}");
                    return EXIT_INVALID_UNC_PATH;
                }

                if (!Directory.Exists(directory))
                {
                    Console.Error.WriteLine($"Error: Target directory does not exist or is not reachable: {directory}");
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("Troubleshooting:");
                    Console.Error.WriteLine("  - Verify the server name is correct and reachable");
                    Console.Error.WriteLine("  - Verify the share name exists on the server");
                    Console.Error.WriteLine("  - Check network connectivity to the file share");
                    Console.Error.WriteLine("  - Ensure Kerberos delegation is working correctly");
                    return EXIT_DIRECTORY_NOT_FOUND;
                }
            }
            else
            {
                // Local path — ensure the directory exists, create it if needed
                string? directory = Path.GetDirectoryName(uncPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    try { Directory.CreateDirectory(directory); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error: Could not create local directory '{directory}': {ex.Message}");
                        return EXIT_IO_ERROR;
                    }
                }
            }

            // Write content to file at UNC path, including timestamp and user identity
            string timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
            string username = identity?.Name ?? "Unknown";
            string fullContent = $"[{timestamp}] Written by: {username}\n{content}\n";

            Console.WriteLine($"Writing to: {uncPath}");
            Console.WriteLine($"Content length: {fullContent.Length} bytes");
            Console.WriteLine();

            File.WriteAllText(uncPath, fullContent);

            // Verify file was created successfully
            if (!File.Exists(uncPath))
            {
                Console.Error.WriteLine($"Error: File was not created successfully: {uncPath}");
                Console.Error.WriteLine();
                Console.Error.WriteLine("Troubleshooting:");
                Console.Error.WriteLine("  - The write operation appeared to succeed but the file is not present");
                Console.Error.WriteLine("  - Check for file system issues on the target share");
                Console.Error.WriteLine("  - Verify sufficient disk space on the target share");
                return EXIT_IO_ERROR;
            }

            Console.WriteLine("SUCCESS: File written successfully!");
            Console.WriteLine($"File path: {uncPath}");
            Console.WriteLine($"File size: {new FileInfo(uncPath).Length} bytes");

            return EXIT_SUCCESS;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.Error.WriteLine($"Error: Access denied - {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Troubleshooting:");
            Console.Error.WriteLine("  - Verify the current user has write permissions to the target path");
            Console.Error.WriteLine("  - Check NTFS permissions on the file share");
            Console.Error.WriteLine("  - Verify the share permissions allow write access");
            Console.Error.WriteLine("  - Ensure Kerberos delegation is working correctly");
            Console.Error.WriteLine("  - Check if the user account is trusted for delegation");
            Console.Error.WriteLine("  - Verify the target SPN is in the allowed delegation list");
            Console.Error.WriteLine("  - Use 'klist' command to verify Kerberos tickets");
            return EXIT_UNAUTHORIZED;
        }
        catch (IOException ex)
        {
            Console.Error.WriteLine($"Error: I/O error occurred - {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Troubleshooting:");
            Console.Error.WriteLine("  - Check network connectivity to the file share");
            Console.Error.WriteLine("  - Verify the file is not locked by another process");
            Console.Error.WriteLine("  - Ensure sufficient disk space on the target share");
            Console.Error.WriteLine("  - Check if the file path is too long (Windows MAX_PATH limit)");
            Console.Error.WriteLine("  - Verify the share is online and accessible");
            return EXIT_IO_ERROR;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: Unexpected error occurred - {ex.Message}");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Stack trace:");
            Console.Error.WriteLine(ex.StackTrace);
            Console.Error.WriteLine();
            Console.Error.WriteLine("Troubleshooting:");
            Console.Error.WriteLine("  - Review the error message and stack trace above");
            Console.Error.WriteLine("  - Check Windows Event Logs for additional details");
            Console.Error.WriteLine("  - Verify all prerequisites are met (domain membership, time sync, etc.)");
            return EXIT_GENERAL_ERROR;
        }
    }
}
