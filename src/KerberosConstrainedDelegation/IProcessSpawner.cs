using Microsoft.Win32.SafeHandles;

namespace KerberosConstrainedDelegation;

/// <summary>
/// Interface for spawning processes with delegated credentials
/// </summary>
public interface IProcessSpawner
{
    /// <summary>
    /// Starts a process with the specified token and arguments
    /// </summary>
    /// <param name="token">Security token to use for the process</param>
    /// <param name="executablePath">Path to the executable</param>
    /// <param name="arguments">Command-line arguments</param>
    /// <param name="timeoutMs">Maximum time to wait for process completion (default: 30 seconds)</param>
    /// <returns>Process execution result including exit code and output</returns>
    /// <exception cref="ArgumentException">Thrown when token is invalid or executable path is invalid</exception>
    /// <exception cref="FileNotFoundException">Thrown when executable file does not exist</exception>
    /// <exception cref="KerberosException">Thrown when process spawning fails</exception>
    ProcessExecutionResult SpawnProcessWithToken(
        SafeAccessTokenHandle token,
        string executablePath,
        string arguments,
        int timeoutMs = 30000);
}
