namespace KerberosConstrainedDelegation;

/// <summary>
/// Interface for managing application configuration
/// </summary>
public interface IConfigurationManager
{
    /// <summary>
    /// Gets the service account credentials for initial authentication
    /// </summary>
    /// <returns>Service account credentials</returns>
    ServiceAccountCredentials GetServiceCredentials();

    /// <summary>
    /// Gets the target user to impersonate
    /// </summary>
    /// <returns>Target username in UPN or DOMAIN\username format</returns>
    string GetTargetUsername();

    /// <summary>
    /// Gets the SPN for the target service
    /// </summary>
    /// <returns>Service Principal Name in format service/host.domain.com</returns>
    string GetTargetServicePrincipalName();

    /// <summary>
    /// Gets the path to the external executable
    /// </summary>
    /// <returns>Absolute path to the executable</returns>
    string GetExternalExecutablePath();

    /// <summary>
    /// Gets the UNC path for file share testing
    /// </summary>
    /// <returns>UNC path in format \\server\share\path</returns>
    string GetFileSharePath();

    /// <summary>
    /// Validates all configuration settings
    /// </summary>
    /// <returns>Validation result indicating success or failure with error message</returns>
    ValidationResult ValidateConfiguration();
}
