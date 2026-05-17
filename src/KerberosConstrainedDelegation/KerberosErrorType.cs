namespace KerberosConstrainedDelegation;

/// <summary>
/// Defines the types of Kerberos-related errors that can occur
/// </summary>
public enum KerberosErrorType
{
    /// <summary>
    /// Service account authentication failed
    /// </summary>
    ServiceAuthenticationFailed,

    /// <summary>
    /// Target user was not found in Active Directory
    /// </summary>
    UserNotFound,

    /// <summary>
    /// Service account is not configured for delegation to the target SPN
    /// </summary>
    DelegationNotConfigured,

    /// <summary>
    /// S4U2Self operation failed
    /// </summary>
    S4U2SelfFailed,

    /// <summary>
    /// S4U2Proxy operation failed
    /// </summary>
    S4U2ProxyFailed,

    /// <summary>
    /// Token creation or validation failed
    /// </summary>
    TokenCreationFailed,

    /// <summary>
    /// Process spawning with delegated credentials failed
    /// </summary>
    ProcessSpawnFailed
}
