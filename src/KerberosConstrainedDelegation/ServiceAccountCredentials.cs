using System.Security;

namespace KerberosConstrainedDelegation;

/// <summary>
/// Represents service account credentials for Kerberos authentication
/// </summary>
public sealed class ServiceAccountCredentials : IDisposable
{
    /// <summary>
    /// Gets the username (without domain)
    /// </summary>
    public string Username { get; init; }

    /// <summary>
    /// Gets the domain name
    /// </summary>
    public string Domain { get; init; }

    /// <summary>
    /// Gets the password as a SecureString
    /// </summary>
    public SecureString Password { get; init; }

    /// <summary>
    /// Gets the fully qualified username (DOMAIN\username)
    /// </summary>
    public string FullyQualifiedUsername => $"{Domain}\\{Username}";

    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the ServiceAccountCredentials class
    /// </summary>
    /// <param name="username">Username without domain</param>
    /// <param name="domain">Domain name</param>
    /// <param name="password">Password as SecureString</param>
    public ServiceAccountCredentials(string username, string domain, SecureString password)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username cannot be null or empty", nameof(username));

        if (string.IsNullOrWhiteSpace(domain))
            throw new ArgumentException("Domain cannot be null or empty", nameof(domain));

        if (password == null || password.Length == 0)
            throw new ArgumentException("Password cannot be null or empty", nameof(password));

        // Validate username doesn't contain invalid characters
        char[] invalidChars = ['\\', '/', ':', '*', '?', '"', '<', '>', '|'];
        if (username.IndexOfAny(invalidChars) >= 0)
            throw new ArgumentException($"Username contains invalid characters: {string.Join(", ", invalidChars)}", nameof(username));

        Username = username;
        Domain = domain;
        Password = password;
    }

    /// <summary>
    /// Disposes the credentials and clears sensitive data
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            Password?.Dispose();
            _disposed = true;
        }
    }
}
