using System.Security.Principal;

namespace KerberosConstrainedDelegation;

/// <summary>
/// Represents user identity information extracted from a Windows token
/// </summary>
public sealed class UserIdentityInfo
{
    /// <summary>
    /// Gets the username
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Gets the domain
    /// </summary>
    public string Domain { get; init; } = string.Empty;

    /// <summary>
    /// Gets the authentication type (e.g., "Kerberos", "NTLM")
    /// </summary>
    public string AuthenticationType { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the user is authenticated
    /// </summary>
    public bool IsAuthenticated { get; init; }

    /// <summary>
    /// Gets the Security Identifier (SID) of the user
    /// </summary>
    public SecurityIdentifier? Sid { get; init; }

    /// <summary>
    /// Gets the fully qualified name in DOMAIN\username format
    /// </summary>
    public string FullyQualifiedName => $"{Domain}\\{Username}";

    /// <summary>
    /// Initializes a new instance of the UserIdentityInfo class
    /// </summary>
    public UserIdentityInfo()
    {
    }

    /// <summary>
    /// Initializes a new instance of the UserIdentityInfo class with specified values
    /// </summary>
    /// <param name="username">The username</param>
    /// <param name="domain">The domain</param>
    /// <param name="authenticationType">The authentication type</param>
    /// <param name="isAuthenticated">Whether the user is authenticated</param>
    /// <param name="sid">The Security Identifier</param>
    /// <exception cref="ArgumentException">Thrown when authenticated user has missing required fields</exception>
    public UserIdentityInfo(string username, string domain, string authenticationType, bool isAuthenticated, SecurityIdentifier? sid)
    {
        if (isAuthenticated)
        {
            if (string.IsNullOrWhiteSpace(username))
                throw new ArgumentException("Username cannot be empty for authenticated user", nameof(username));
            if (string.IsNullOrWhiteSpace(domain))
                throw new ArgumentException("Domain cannot be empty for authenticated user", nameof(domain));
            if (sid == null)
                throw new ArgumentException("SID cannot be null for authenticated user", nameof(sid));
        }

        Username = username ?? string.Empty;
        Domain = domain ?? string.Empty;
        AuthenticationType = authenticationType ?? string.Empty;
        IsAuthenticated = isAuthenticated;
        Sid = sid;
    }
}
