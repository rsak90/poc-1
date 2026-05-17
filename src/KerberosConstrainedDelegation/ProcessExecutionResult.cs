namespace KerberosConstrainedDelegation;

/// <summary>
/// Represents the result of a process execution
/// </summary>
public sealed class ProcessExecutionResult
{
    /// <summary>
    /// Gets the exit code of the process
    /// </summary>
    public int ExitCode { get; init; }

    /// <summary>
    /// Gets the standard output from the process
    /// </summary>
    public string StandardOutput { get; init; } = string.Empty;

    /// <summary>
    /// Gets the standard error from the process
    /// </summary>
    public string StandardError { get; init; } = string.Empty;

    /// <summary>
    /// Gets a value indicating whether the process timed out
    /// </summary>
    public bool TimedOut { get; init; }

    /// <summary>
    /// Gets the execution time of the process
    /// </summary>
    public TimeSpan ExecutionTime { get; init; }

    /// <summary>
    /// Gets a value indicating whether the process execution was successful
    /// </summary>
    public bool IsSuccess => ExitCode == 0 && !TimedOut;

    /// <summary>
    /// Initializes a new instance of the ProcessExecutionResult class
    /// </summary>
    public ProcessExecutionResult()
    {
    }

    /// <summary>
    /// Initializes a new instance of the ProcessExecutionResult class with specified values
    /// </summary>
    /// <param name="exitCode">The exit code of the process</param>
    /// <param name="standardOutput">The standard output from the process</param>
    /// <param name="standardError">The standard error from the process</param>
    /// <param name="timedOut">Whether the process timed out</param>
    /// <param name="executionTime">The execution time of the process</param>
    /// <exception cref="ArgumentException">Thrown when executionTime is negative</exception>
    public ProcessExecutionResult(int exitCode, string standardOutput, string standardError, bool timedOut, TimeSpan executionTime)
    {
        if (executionTime < TimeSpan.Zero)
            throw new ArgumentException("Execution time cannot be negative", nameof(executionTime));

        ExitCode = exitCode;
        StandardOutput = standardOutput ?? string.Empty;
        StandardError = standardError ?? string.Empty;
        TimedOut = timedOut;
        ExecutionTime = executionTime;
    }
}
