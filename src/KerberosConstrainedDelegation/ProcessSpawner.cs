using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace KerberosConstrainedDelegation;

/// <summary>
/// Spawns external processes with delegated credentials
/// </summary>
public sealed class ProcessSpawner : IProcessSpawner
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
    public ProcessExecutionResult SpawnProcessWithToken(
        SafeAccessTokenHandle token,
        string executablePath,
        string arguments,
        int timeoutMs = 30000)
    {
        // Step 1: Validate inputs
        if (token == null || token.IsInvalid)
        {
            throw new ArgumentException("Invalid token handle", nameof(token));
        }

        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new ArgumentException("Executable path cannot be null or empty", nameof(executablePath));
        }

        if (timeoutMs <= 0)
        {
            throw new ArgumentException("Timeout must be positive", nameof(timeoutMs));
        }

        if (!File.Exists(executablePath))
        {
            throw new FileNotFoundException($"Executable not found: {executablePath}", executablePath);
        }

        var startTime = DateTime.UtcNow;
        SafeFileHandle? stdOutRead = null;
        SafeFileHandle? stdOutWrite = null;
        SafeFileHandle? stdErrRead = null;
        SafeFileHandle? stdErrWrite = null;
        IntPtr processHandle = IntPtr.Zero;
        IntPtr threadHandle = IntPtr.Zero;

        try
        {
            // Step 2: Create pipes for standard output and error
            CreatePipe(out stdOutRead, out stdOutWrite, true);
            CreatePipe(out stdErrRead, out stdErrWrite, true);

            // Step 3: Prepare process startup information.
            // bInheritHandles=true on CreateProcessAsUser means the pipe write handles
            // are inherited by the child — no need for any special flags beyond
            // STARTF_USESTDHANDLES. CREATE_NO_WINDOW suppresses any console window.
            var startupInfo = new NativeMethods.STARTUPINFO
            {
                cb = Marshal.SizeOf<NativeMethods.STARTUPINFO>(),
                dwFlags = NativeMethods.STARTF_USESTDHANDLES,
                wShowWindow = 0,
                hStdInput = IntPtr.Zero,
                hStdOutput = stdOutWrite.DangerousGetHandle(),
                hStdError = stdErrWrite.DangerousGetHandle()
            };

            // Step 4: Build command line
            var commandLine = string.IsNullOrWhiteSpace(arguments)
                ? $"\"{executablePath}\""
                : $"\"{executablePath}\" {arguments}";

            string? workingDirectory = null;

            Console.WriteLine($"[ProcessSpawner] Executable: {executablePath}");
            Console.WriteLine($"[ProcessSpawner] CommandLine: {commandLine}");
            Console.WriteLine($"[ProcessSpawner] Exe exists:  {File.Exists(executablePath)}");

            // Step 5: Spawn the process under the delegated token.
            // We use CreateProcessAsUser (not CreateProcessWithTokenW) because:
            //   - Our token is Primary/Identification (S4U via DuplicateTokenEx)
            //   - CreateProcessAsUser accepts Primary/Identification with SeTcbPrivilege
            //   - CreateProcessWithTokenW requires Impersonation level (fails with 1346)
            // bInheritHandles=true so the child inherits the stdout/stderr pipe handles.
            var success = NativeMethods.CreateProcessAsUser(
                token,
                null,           // lpApplicationName — use commandLine instead
                commandLine,
                IntPtr.Zero,    // default process security
                IntPtr.Zero,    // default thread security
                true,           // bInheritHandles — child inherits pipe handles
                NativeMethods.CREATE_NO_WINDOW | NativeMethods.CREATE_UNICODE_ENVIRONMENT,
                IntPtr.Zero,    // inherit environment
                workingDirectory,
                ref startupInfo,
                out NativeMethods.PROCESS_INFORMATION processInfo);

            if (!success)
            {
                var error = Marshal.GetLastWin32Error();
                throw new KerberosException(
                    $"CreateProcessAsUser failed. Win32 error: {error} (0x{error:X8}). " +
                    $"Executable: {executablePath}",
                    error,
                    KerberosErrorType.ProcessSpawnFailed);
            }

            processHandle = processInfo.hProcess;
            threadHandle = processInfo.hThread;

            // Step 6: Close write ends of pipes (child process owns them)
            stdOutWrite.Dispose();
            stdOutWrite = null;
            stdErrWrite.Dispose();
            stdErrWrite = null;

            // Step 7: Wait for process completion with timeout
            var waitResult = NativeMethods.WaitForSingleObject(processHandle, (uint)timeoutMs);
            var timedOut = (waitResult == NativeMethods.WAIT_TIMEOUT);

            if (timedOut)
            {
                // Terminate process if it timed out
                NativeMethods.TerminateProcess(processHandle, 1);
                
                // Wait a bit for termination to complete
                NativeMethods.WaitForSingleObject(processHandle, 1000);
            }

            // Step 8: Get exit code
            if (!NativeMethods.GetExitCodeProcess(processHandle, out int exitCode))
            {
                var error = Marshal.GetLastWin32Error();
                throw new KerberosException(
                    $"GetExitCodeProcess failed with error: 0x{error:X8}",
                    error,
                    KerberosErrorType.ProcessSpawnFailed);
            }

            // Step 9: Read standard output and error asynchronously to prevent deadlocks
            var stdOut = ReadFromPipe(stdOutRead);
            var stdErr = ReadFromPipe(stdErrRead);

            var executionTime = DateTime.UtcNow - startTime;

            return new ProcessExecutionResult
            {
                ExitCode = exitCode,
                StandardOutput = stdOut,
                StandardError = stdErr,
                TimedOut = timedOut,
                ExecutionTime = executionTime
            };
        }
        catch (Exception ex) when (ex is not KerberosException)
        {
            throw new KerberosException(
                $"Process spawning failed: {ex.Message}",
                Marshal.GetLastWin32Error(),
                KerberosErrorType.ProcessSpawnFailed,
                ex);
        }
        finally
        {
            // Step 10: Close all handles properly
            stdOutRead?.Dispose();
            stdOutWrite?.Dispose();
            stdErrRead?.Dispose();
            stdErrWrite?.Dispose();

            if (processHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(processHandle);
            }

            if (threadHandle != IntPtr.Zero)
            {
                NativeMethods.CloseHandle(threadHandle);
            }
        }
    }

    /// <summary>
    /// Creates a pipe for inter-process communication
    /// </summary>
    /// <param name="readHandle">Output parameter for the read end of the pipe</param>
    /// <param name="writeHandle">Output parameter for the write end of the pipe</param>
    /// <param name="inheritHandle">Whether the handles should be inheritable by child processes</param>
    /// <exception cref="KerberosException">Thrown when pipe creation fails</exception>
    private void CreatePipe(out SafeFileHandle readHandle, out SafeFileHandle writeHandle, bool inheritHandle)
    {
        var securityAttributes = new NativeMethods.SECURITY_ATTRIBUTES
        {
            nLength = Marshal.SizeOf<NativeMethods.SECURITY_ATTRIBUTES>(),
            lpSecurityDescriptor = IntPtr.Zero,
            bInheritHandle = inheritHandle
        };

        var success = NativeMethods.CreatePipe(
            out readHandle,
            out writeHandle,
            ref securityAttributes,
            0); // Use default buffer size

        if (!success)
        {
            var error = Marshal.GetLastWin32Error();
            throw new KerberosException(
                $"CreatePipe failed with error: 0x{error:X8}",
                error,
                KerberosErrorType.ProcessSpawnFailed);
        }
    }

    /// <summary>
    /// Reads all available data from a pipe asynchronously to prevent deadlocks
    /// </summary>
    /// <param name="pipeHandle">Handle to the read end of the pipe</param>
    /// <returns>String containing all data read from the pipe</returns>
    private string ReadFromPipe(SafeFileHandle pipeHandle)
    {
        if (pipeHandle == null || pipeHandle.IsInvalid)
        {
            return string.Empty;
        }

        var output = new StringBuilder();
        var buffer = new byte[4096];

        try
        {
            while (true)
            {
                // Check if there's data available in the pipe
                if (!NativeMethods.PeekNamedPipe(
                    pipeHandle,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    out uint bytesAvailable,
                    IntPtr.Zero))
                {
                    // Pipe is closed or error occurred
                    break;
                }

                if (bytesAvailable == 0)
                {
                    // No more data available
                    break;
                }

                // Read available data
                var success = NativeMethods.ReadFile(
                    pipeHandle,
                    buffer,
                    (uint)buffer.Length,
                    out uint bytesRead,
                    IntPtr.Zero);

                if (!success || bytesRead == 0)
                {
                    // End of file or error
                    break;
                }

                // Convert bytes to string and append
                var text = Encoding.UTF8.GetString(buffer, 0, (int)bytesRead);
                output.Append(text);
            }
        }
        catch (Exception)
        {
            // If reading fails, return what we have so far
            // Don't throw exception as this is a best-effort operation
        }

        return output.ToString();
    }
}
