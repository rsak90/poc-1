using Microsoft.Win32.SafeHandles;

namespace KerberosConstrainedDelegation;

/// <summary>
/// Interface for managing Kerberos tokens using constrained delegation
/// </summary>
public interface IKerberosTokenManager : IDisposable
{
    /// <summary>
    /// Obtains a delegated Kerberos token for the specified user and target service
    /// </summary>
    /// <param name="username">User to impersonate (UPN or DOMAIN\username format)</param>
    /// <param name="targetServicePrincipalName">SPN of the target service for delegation</param>
    /// <returns>Windows token handle with delegated credentials</returns>
    SafeAccessTokenHandle GetDelegatedToken(string username, string targetServicePrincipalName);

    /// <summary>
    /// Validates that the service account is properly configured for constrained delegation
    /// </summary>
    /// <returns>True if delegation is properly configured</returns>
    bool ValidateDelegationConfiguration();
}
