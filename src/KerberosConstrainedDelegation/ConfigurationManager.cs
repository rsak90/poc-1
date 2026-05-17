using System.Security;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace KerberosConstrainedDelegation;

/// <summary>
/// Manages application configuration from app.config and command-line arguments
/// </summary>
public sealed class ConfigurationManager : IConfigurationManager, IDisposable
{
    private readonly IConfiguration _configuration;
    private ServiceAccountCredentials? _serviceCredentials;
    private bool _disposed;

    // Configuration keys
    private const string ServiceUsernameKey = "ServiceAccount:Username";
    private const string ServiceDomainKey = "ServiceAccount:Domain";
    private const string ServicePasswordKey = "ServiceAccount:Password";
    private const string TargetUsernameKey = "TargetUser:Username";
    private const string TargetSpnKey = "TargetService:Spn";
    private const string ExecutablePathKey = "ExternalExecutable:Path";
    private const string FileSharePathKey = "FileShare:Path";
    private const string TimeoutKey = "Execution:TimeoutSeconds";

    /// <summary>
    /// Initializes a new instance of the ConfigurationManager class
    /// </summary>
    /// <param name="configuration">Configuration source (from app.config or command-line)</param>
    public ConfigurationManager(IConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Creates a ConfigurationManager from appsettings.json and command-line arguments
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    /// <returns>ConfigurationManager instance</returns>
    public static ConfigurationManager CreateFromArgs(string[] args)
    {
        // Use AppContext.BaseDirectory to get the executable's directory
        // This is important for Windows services where Directory.GetCurrentDirectory() 
        // returns the service's working directory (typically C:\Windows\System32)
        var baseDirectory = AppContext.BaseDirectory;

        var configuration = new ConfigurationBuilder()
            .SetBasePath(baseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddCommandLine(args)
            .Build();

        return new ConfigurationManager(configuration);
    }

    /// <summary>
    /// Gets the service account credentials for initial authentication
    /// </summary>
    public ServiceAccountCredentials GetServiceCredentials()
    {
        if (_serviceCredentials != null)
            return _serviceCredentials;

        var username = _configuration[ServiceUsernameKey];
        var domain = _configuration[ServiceDomainKey];
        var password = _configuration[ServicePasswordKey];

        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException($"Configuration key '{ServiceUsernameKey}' is missing or empty");

        if (string.IsNullOrWhiteSpace(domain))
            throw new InvalidOperationException($"Configuration key '{ServiceDomainKey}' is missing or empty");

        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException($"Configuration key '{ServicePasswordKey}' is missing or empty");

        // Convert password to SecureString
        var securePassword = new SecureString();
        foreach (char c in password)
        {
            securePassword.AppendChar(c);
        }
        securePassword.MakeReadOnly();

        _serviceCredentials = new ServiceAccountCredentials(username, domain, securePassword);
        return _serviceCredentials;
    }

    /// <summary>
    /// Gets the target user to impersonate
    /// </summary>
    public string GetTargetUsername()
    {
        var username = _configuration[TargetUsernameKey];
        if (string.IsNullOrWhiteSpace(username))
            throw new InvalidOperationException($"Configuration key '{TargetUsernameKey}' is missing or empty");

        return username;
    }

    /// <summary>
    /// Gets the SPN for the target service
    /// </summary>
    public string GetTargetServicePrincipalName()
    {
        var spn = _configuration[TargetSpnKey];
        if (string.IsNullOrWhiteSpace(spn))
            throw new InvalidOperationException($"Configuration key '{TargetSpnKey}' is missing or empty");

        return spn;
    }

    /// <summary>
    /// Gets the path to the external executable
    /// </summary>
    public string GetExternalExecutablePath()
    {
        var path = _configuration[ExecutablePathKey];
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException($"Configuration key '{ExecutablePathKey}' is missing or empty");

        return path;
    }

    /// <summary>
    /// Gets the UNC path for file share testing
    /// </summary>
    public string GetFileSharePath()
    {
        var path = _configuration[FileSharePathKey];
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException($"Configuration key '{FileSharePathKey}' is missing or empty");

        return path;
    }

    /// <summary>
    /// Gets the execution timeout in seconds (default: 30)
    /// </summary>
    public int GetTimeoutSeconds()
    {
        var timeoutStr = _configuration[TimeoutKey];
        if (string.IsNullOrWhiteSpace(timeoutStr))
            return 30; // Default timeout

        if (int.TryParse(timeoutStr, out int timeout) && timeout > 0)
            return timeout;

        return 30; // Default timeout if parsing fails
    }

    /// <summary>
    /// Validates all configuration settings
    /// </summary>
    public ValidationResult ValidateConfiguration()
    {
        try
        {
            // Validate service account credentials
            var serviceUsername = _configuration[ServiceUsernameKey];
            if (string.IsNullOrWhiteSpace(serviceUsername))
                return ValidationResult.Failure($"Service account username is missing (key: {ServiceUsernameKey})");

            var serviceDomain = _configuration[ServiceDomainKey];
            if (string.IsNullOrWhiteSpace(serviceDomain))
                return ValidationResult.Failure($"Service account domain is missing (key: {ServiceDomainKey})");

            var servicePassword = _configuration[ServicePasswordKey];
            if (string.IsNullOrWhiteSpace(servicePassword))
                return ValidationResult.Failure($"Service account password is missing (key: {ServicePasswordKey})");

            // Validate service username format
            char[] invalidChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];
            if (serviceUsername.IndexOfAny(invalidChars) >= 0)
                return ValidationResult.Failure($"Service account username contains invalid characters: {string.Join(", ", invalidChars)}");

            // Validate target username
            var targetUsername = _configuration[TargetUsernameKey];
            if (string.IsNullOrWhiteSpace(targetUsername))
                return ValidationResult.Failure($"Target username is missing (key: {TargetUsernameKey})");

            // Validate target username format (UPN or DOMAIN\username)
            if (!IsValidUsernameFormat(targetUsername))
                return ValidationResult.Failure($"Target username must be in UPN format (user@domain.com) or DOMAIN\\username format");

            // Validate target SPN
            var targetSpn = _configuration[TargetSpnKey];
            if (string.IsNullOrWhiteSpace(targetSpn))
                return ValidationResult.Failure($"Target SPN is missing (key: {TargetSpnKey})");

            // Validate SPN format (service/host.domain.com)
            if (!IsValidSpnFormat(targetSpn))
                return ValidationResult.Failure($"Target SPN must be in format 'service/host.domain.com' (e.g., 'cifs/fileserver.contoso.com')");

            // Validate executable path
            var executablePath = _configuration[ExecutablePathKey];
            if (string.IsNullOrWhiteSpace(executablePath))
                return ValidationResult.Failure($"External executable path is missing (key: {ExecutablePathKey})");

            // Check if executable exists
            if (!File.Exists(executablePath))
                return ValidationResult.Failure($"External executable not found at path: {executablePath}");

            // Validate file share path
            var fileSharePath = _configuration[FileSharePathKey];
            if (string.IsNullOrWhiteSpace(fileSharePath))
                return ValidationResult.Failure($"File share path is missing (key: {FileSharePathKey})");

            // Validate UNC path format
            if (!IsValidUncPath(fileSharePath))
                return ValidationResult.Failure($"File share path must be a valid UNC path (e.g., \\\\server\\share\\file.txt)");

            // Validate timeout
            var timeoutStr = _configuration[TimeoutKey];
            if (!string.IsNullOrWhiteSpace(timeoutStr))
            {
                if (!int.TryParse(timeoutStr, out int timeout) || timeout <= 0)
                    return ValidationResult.Failure($"Timeout must be a positive integer (key: {TimeoutKey})");
            }

            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure($"Configuration validation error: {ex.Message}");
        }
    }

    /// <summary>
    /// Validates username format (UPN or DOMAIN\username)
    /// </summary>
    private static bool IsValidUsernameFormat(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return false;

        // Check for UPN format (user@domain.com)
        if (username.Contains('@'))
        {
            var parts = username.Split('@');
            return parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]);
        }

        // Check for DOMAIN\username format
        if (username.Contains('\\'))
        {
            var parts = username.Split('\\');
            return parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]);
        }

        return false;
    }

    /// <summary>
    /// Validates SPN format (service/host.domain.com)
    /// </summary>
    private static bool IsValidSpnFormat(string spn)
    {
        if (string.IsNullOrWhiteSpace(spn))
            return false;

        // SPN format: service/host.domain.com or service/host.domain.com:port
        // Examples: cifs/fileserver.contoso.com, http/webapp.contoso.com:8080
        var regex = new Regex(@"^[a-zA-Z0-9_-]+/[a-zA-Z0-9.-]+(:[0-9]+)?$", RegexOptions.Compiled);
        return regex.IsMatch(spn);
    }

    /// <summary>
    /// Validates UNC path format (\\server\share\path)
    /// </summary>
    private static bool IsValidUncPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // UNC path must start with \\
        if (!path.StartsWith(@"\\"))
            return false;

        // Basic validation: \\server\share\...
        var parts = path.Substring(2).Split('\\');
        return parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]);
    }

    /// <summary>
    /// Disposes the configuration manager and clears sensitive data
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _serviceCredentials?.Dispose();
            _disposed = true;
        }
    }
}
