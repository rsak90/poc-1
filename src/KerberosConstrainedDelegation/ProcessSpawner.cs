using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;
using Serilog;
using ILogger = Serilog.ILogger;

namespace KerberosConstrainedDelegation;

/// <summary>
/// Spawns external processes with delegated credentials
/// </summary>
public sealed class ProcessSpawner : IProcessSpawner
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of ProcessSpawner.
    /// </summary>
    /// <param name="logger">
    /// Optional Serilog logger. Falls back to the global Log.Logger when null.
    /// </param>
    public ProcessSpawner(ILogger? logger = null)
    {
        _logger = (logger ?? Log.Logger).ForContext<ProcessSpawner>();
    }

    /// <summary>
    /// Starts a process with the specified token and arguments
    /// </summary>
    public ProcessExecutionResult SpawnProcessWithToken(
        SafeAccessTokenHandle token,
        string executablePath,
        string arguments,
        int timeoutMs = 30000)
    {
        _logger.Information("[ProcessSpawner] SpawnProcessWithToken called");
        _logger.Information("[ProcessSpawner]   Executable : {Executable}", executablePath);
        _logger.Information("[ProcessSpawner]   Arguments  : {Arguments}", arguments);
        _logger.Information("[ProcessSpawner]   TimeoutMs  : {TimeoutMs}", timeoutMs);
        _logger.Information("[ProcessSpawner]   Token valid: {TokenValid}", token != null && !token.IsInvalid);

        // Step 1: Validate inputs
        if (token == null || token.IsInvalid)
            throw new ArgumentException("Invalid token handle", nameof(token));

        if (string.IsNullOrWhiteSpace(executablePath))
            throw new ArgumentException("Executable path cannot be null or empty", nameof(executablePath));

        if (timeoutMs <= 0)
            throw new ArgumentException("Timeout must be positive", nameof(timeoutMs));

        if (!File.Exists(executablePath))
        {
            _logger.Error("[ProcessSpawner] Executable not found: {Executable}", executablePath);
            throw new FileNotFoundException($"Executable not found: {executablePath}", executablePath);
        }

        _logger.Information("[ProcessSpawner] Exe exists on disk: true");

        // Log token type/level before attempting spawn
        LogTokenStats(token);

        var startTime = DateTime.UtcNow;
        SafeFileHandle? stdOutRead = null;
        SafeFileHandle? stdOutWrite = null;
        SafeFileHandle? stdErrRead = null;
        SafeFileHandle? stdErrWrite = null;
        IntPtr processHandle = IntPtr.Zero;
        IntPtr threadHandle = IntPtr.Zero;

        try
        {
            // Step 2: Create pipes for stdout and stderr
            _logger.Information("[ProcessSpawner] Creating stdout/stderr pipes...");
            CreatePipe(out stdOutRead, out stdOutWrite, true);
            CreatePipe(out stdErrRead, out stdErrWrite, true);
            _logger.Information("[ProcessSpawner] Pipes created successfully");

            // Step 3: Build STARTUPINFO
            var startupInfo = new NativeMethods.STARTUPINFO
            {
                cb         = Marshal.SizeOf<NativeMethods.STARTUPINFO>(),
                dwFlags    = NativeMethods.STARTF_USESTDHANDLES,
                wShowWindow = 0,
                hStdInput  = IntPtr.Zero,
                hStdOutput = stdOutWrite.DangerousGetHandle(),
                hStdError  = stdErrWrite.DangerousGetHandle()
            };

            _logger.Information("[ProcessSpawner] STARTUPINFO.cb      = {Cb}", startupInfo.cb);
            _logger.Information("[ProcessSpawner] STARTUPINFO.dwFlags = 0x{Flags:X}", startupInfo.dwFlags);

            // Step 4: Build command line
            var commandLine = string.IsNullOrWhiteSpace(arguments)
                ? $"\"{executablePath}\""
                : $"\"{executablePath}\" {arguments}";

            string? workingDirectory = null;

            _logger.Information("[ProcessSpawner] CommandLine      : {CommandLine}", commandLine);
            _logger.Information("[ProcessSpawner] WorkingDirectory : {WorkingDir}", workingDirectory ?? "(inherited)");
            _logger.Information("[ProcessSpawner] CreationFlags    : CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT");

            // Step 5: Call CreateProcessWithTokenW
            _logger.Information("[ProcessSpawner] Calling CreateProcessWithTokenW...");

            var success = NativeMethods.CreateProcessWithTokenW(
                token,
                0,
                null,
                commandLine,
                NativeMethods.CREATE_NO_WINDOW | NativeMethods.CREATE_UNICODE_ENVIRONMENT,
                IntPtr.Zero,
                workingDirectory,
                ref startupInfo,
                out NativeMethods.PROCESS_INFORMATION processInfo);

            if (!success)
            {
                var error = Marshal.GetLastWin32Error();
                _logger.Error("[ProcessSpawner] CreateProcessWithTokenW FAILED");
                _logger.Error("[ProcessSpawner]   Win32 error : {Error} (0x{ErrorHex:X8})", error, error);
                _logger.Error("[ProcessSpawner]   Executable  : {Executable}", executablePath);
                _logger.Error("[ProcessSpawner]   CommandLine : {CommandLine}", commandLine);
                _logger.Error("[ProcessSpawner]   Token valid : {TokenValid}", !token.IsInvalid);
                throw new KerberosException(
                    $"CreateProcessWithTokenW failed. Win32 error: {error} (0x{error:X8}). Executable: {executablePath}",
                    error,
                    KerberosErrorType.ProcessSpawnFailed);
            }

            processHandle = processInfo.hProcess;
            threadHandle  = processInfo.hThread;

            _logger.Information("[ProcessSpawner] CreateProcessWithTokenW succeeded");
            _logger.Information("[ProcessSpawner]   PID         : {Pid}", processInfo.dwProcessId);
            _logger.Information("[ProcessSpawner]   TID         : {Tid}", processInfo.dwThreadId);

            // Step 6: Close write ends of pipes — child owns them now
            stdOutWrite.Dispose(); stdOutWrite = null;
            stdErrWrite.Dispose(); stdErrWrite = null;
            _logger.Information("[ProcessSpawner] Write pipe ends closed");

            // Step 7: Wait for process completion
            _logger.Information("[ProcessSpawner] Waiting for process (timeout {TimeoutMs} ms)...", timeoutMs);
            var waitResult = NativeMethods.WaitForSingleObject(processHandle, (uint)timeoutMs);
            var timedOut   = waitResult == NativeMethods.WAIT_TIMEOUT;

            if (timedOut)
            {
                _logger.Warning("[ProcessSpawner] Process timed out — terminating");
                NativeMethods.TerminateProcess(processHandle, 1);
                NativeMethods.WaitForSingleObject(processHandle, 1000);
            }
            else
            {
                _logger.Information("[ProcessSpawner] Process completed (WaitForSingleObject result: 0x{Result:X})", waitResult);
            }

            // Step 8: Get exit code
            if (!NativeMethods.GetExitCodeProcess(processHandle, out int exitCode))
            {
                var error = Marshal.GetLastWin32Error();
                throw new KerberosException(
                    $"GetExitCodeProcess failed: 0x{error:X8}", error, KerberosErrorType.ProcessSpawnFailed);
            }

            _logger.Information("[ProcessSpawner] Exit code: {ExitCode}", exitCode);

            // Step 9: Read stdout/stderr
            var stdOut = ReadFromPipe(stdOutRead);
            var stdErr = ReadFromPipe(stdErrRead);

            if (!string.IsNullOrWhiteSpace(stdOut))
                _logger.Information("[ProcessSpawner] stdout:\n{StdOut}", stdOut.TrimEnd());
            if (!string.IsNullOrWhiteSpace(stdErr))
                _logger.Warning("[ProcessSpawner] stderr:\n{StdErr}", stdErr.TrimEnd());

            var executionTime = DateTime.UtcNow - startTime;
            _logger.Information("[ProcessSpawner] Execution time: {ElapsedMs} ms", (int)executionTime.TotalMilliseconds);

            return new ProcessExecutionResult
            {
                ExitCode        = exitCode,
                StandardOutput  = stdOut,
                StandardError   = stdErr,
                TimedOut        = timedOut,
                ExecutionTime   = executionTime
            };
        }
        catch (Exception ex) when (ex is not KerberosException)
        {
            _logger.Error(ex, "[ProcessSpawner] Unexpected exception: {ErrorMessage}", ex.Message);
            throw new KerberosException(
                $"Process spawning failed: {ex.Message}",
                Marshal.GetLastWin32Error(),
                KerberosErrorType.ProcessSpawnFailed,
                ex);
        }
        finally
        {
            stdOutRead?.Dispose();
            stdOutWrite?.Dispose();
            stdErrRead?.Dispose();
            stdErrWrite?.Dispose();

            if (processHandle != IntPtr.Zero) NativeMethods.CloseHandle(processHandle);
            if (threadHandle  != IntPtr.Zero) NativeMethods.CloseHandle(threadHandle);
        }
    }

    /// <summary>
    /// Logs TOKEN_STATISTICS for the token being passed to CreateProcessWithTokenW.
    /// Uses only GetTokenInformation — avoids WindowsIdentity which throws on S4U tokens.
    /// </summary>
    private void LogTokenStats(SafeAccessTokenHandle token)
    {
        try
        {
            NativeMethods.GetTokenInformation(token, NativeMethods.TokenStatistics,
                IntPtr.Zero, 0, out int needed);
            var buf = Marshal.AllocHGlobal(needed);
            try
            {
                if (!NativeMethods.GetTokenInformation(token, NativeMethods.TokenStatistics,
                        buf, needed, out _))
                {
                    _logger.Warning("[ProcessSpawner] GetTokenInformation(TokenStatistics) failed: Win32 {Err}",
                        Marshal.GetLastWin32Error());
                    return;
                }

                var stats = Marshal.PtrToStructure<NativeMethods.TOKEN_STATISTICS>(buf);
                string tokenType = stats.TokenType switch
                {
                    1 => "Primary",
                    2 => "Impersonation",
                    _ => $"Unknown({stats.TokenType})"
                };
                string impLevel = stats.ImpersonationLevel switch
                {
                    0 => "Anonymous",
                    1 => "Identification",
                    2 => "Impersonation",
                    3 => "Delegation",
                    _ => $"Unknown({stats.ImpersonationLevel})"
                };

                _logger.Information("[ProcessSpawner] Token stats: Type={TokenType} Level={ImpLevel} LogonId={LogonHigh:X8}:{LogonLow:X8}",
                    tokenType, impLevel,
                    stats.AuthenticationId.HighPart, stats.AuthenticationId.LowPart);
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
        catch (Exception ex)
        {
            _logger.Warning("[ProcessSpawner] LogTokenStats failed: {ErrorMessage}", ex.Message);
        }
    }

    private void CreatePipe(out SafeFileHandle readHandle, out SafeFileHandle writeHandle, bool inheritHandle)
    {
        var sa = new NativeMethods.SECURITY_ATTRIBUTES
        {
            nLength              = Marshal.SizeOf<NativeMethods.SECURITY_ATTRIBUTES>(),
            lpSecurityDescriptor = IntPtr.Zero,
            bInheritHandle       = inheritHandle
        };

        if (!NativeMethods.CreatePipe(out readHandle, out writeHandle, ref sa, 0))
        {
            var error = Marshal.GetLastWin32Error();
            _logger.Error("[ProcessSpawner] CreatePipe failed: Win32 {Error} (0x{ErrorHex:X8})", error, error);
            throw new KerberosException(
                $"CreatePipe failed: 0x{error:X8}", error, KerberosErrorType.ProcessSpawnFailed);
        }
    }

    private string ReadFromPipe(SafeFileHandle pipeHandle)
    {
        if (pipeHandle == null || pipeHandle.IsInvalid) return string.Empty;

        var output = new StringBuilder();
        var buffer = new byte[4096];

        try
        {
            while (true)
            {
                if (!NativeMethods.PeekNamedPipe(pipeHandle, IntPtr.Zero, 0,
                        IntPtr.Zero, out uint bytesAvailable, IntPtr.Zero))
                    break;

                if (bytesAvailable == 0) break;

                if (!NativeMethods.ReadFile(pipeHandle, buffer, (uint)buffer.Length,
                        out uint bytesRead, IntPtr.Zero) || bytesRead == 0)
                    break;

                output.Append(Encoding.UTF8.GetString(buffer, 0, (int)bytesRead));
            }
        }
        catch { /* best-effort */ }

        return output.ToString();
    }
}
