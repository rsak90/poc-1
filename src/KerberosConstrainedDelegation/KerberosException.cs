namespace KerberosConstrainedDelegation;

/// <summary>
/// Represents errors that occur during Kerberos constrained delegation operations
/// </summary>
public sealed class KerberosException : Exception
{
    /// <summary>
    /// Gets the Win32 error code associated with this exception
    /// </summary>
    public int Win32ErrorCode { get; init; }

    /// <summary>
    /// Gets the type of Kerberos error that occurred
    /// </summary>
    public KerberosErrorType ErrorType { get; init; }

    /// <summary>
    /// Initializes a new instance of the KerberosException class
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="win32ErrorCode">The Win32 error code</param>
    /// <param name="errorType">The type of Kerberos error</param>
    public KerberosException(string message, int win32ErrorCode, KerberosErrorType errorType)
        : base(message)
    {
        Win32ErrorCode = win32ErrorCode;
        ErrorType = errorType;
    }

    /// <summary>
    /// Initializes a new instance of the KerberosException class with an inner exception
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="win32ErrorCode">The Win32 error code</param>
    /// <param name="errorType">The type of Kerberos error</param>
    /// <param name="innerException">The inner exception</param>
    public KerberosException(string message, int win32ErrorCode, KerberosErrorType errorType, Exception innerException)
        : base(message, innerException)
    {
        Win32ErrorCode = win32ErrorCode;
        ErrorType = errorType;
    }
}
