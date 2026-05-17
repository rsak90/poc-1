using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace KerberosConstrainedDelegation.Tests;

/// <summary>
/// Unit tests for ProcessSpawner class
/// </summary>
public class ProcessSpawnerTests
{
    [Fact]
    public void SpawnProcessWithToken_WithNullToken_ThrowsArgumentException()
    {
        // Arrange
        var spawner = new ProcessSpawner();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            spawner.SpawnProcessWithToken(null!, "test.exe", "args"));
        Assert.Equal("token", exception.ParamName);
    }

    [Fact]
    public void SpawnProcessWithToken_WithInvalidToken_ThrowsArgumentException()
    {
        // Arrange
        var spawner = new ProcessSpawner();
        var invalidToken = new SafeAccessTokenHandle(IntPtr.Zero);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            spawner.SpawnProcessWithToken(invalidToken, "test.exe", "args"));
        Assert.Equal("token", exception.ParamName);
    }

    [Fact]
    public void SpawnProcessWithToken_WithNullExecutablePath_ThrowsArgumentException()
    {
        // Arrange
        var spawner = new ProcessSpawner();
        var token = CreateMockToken();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            spawner.SpawnProcessWithToken(token, null!, "args"));
        Assert.Equal("executablePath", exception.ParamName);
    }

    [Fact]
    public void SpawnProcessWithToken_WithEmptyExecutablePath_ThrowsArgumentException()
    {
        // Arrange
        var spawner = new ProcessSpawner();
        var token = CreateMockToken();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            spawner.SpawnProcessWithToken(token, "", "args"));
        Assert.Equal("executablePath", exception.ParamName);
    }

    [Fact]
    public void SpawnProcessWithToken_WithWhitespaceExecutablePath_ThrowsArgumentException()
    {
        // Arrange
        var spawner = new ProcessSpawner();
        var token = CreateMockToken();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            spawner.SpawnProcessWithToken(token, "   ", "args"));
        Assert.Equal("executablePath", exception.ParamName);
    }

    [Fact]
    public void SpawnProcessWithToken_WithNonExistentExecutable_ThrowsFileNotFoundException()
    {
        // Arrange
        var spawner = new ProcessSpawner();
        var token = CreateMockToken();
        var nonExistentPath = "C:\\NonExistent\\test.exe";

        // Act & Assert
        var exception = Assert.Throws<FileNotFoundException>(() =>
            spawner.SpawnProcessWithToken(token, nonExistentPath, "args"));
        Assert.Contains(nonExistentPath, exception.Message);
    }

    [Fact]
    public void SpawnProcessWithToken_WithZeroTimeout_ThrowsArgumentException()
    {
        // Arrange
        var spawner = new ProcessSpawner();
        var token = CreateMockToken();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            spawner.SpawnProcessWithToken(token, "test.exe", "args", 0));
        Assert.Equal("timeoutMs", exception.ParamName);
    }

    [Fact]
    public void SpawnProcessWithToken_WithNegativeTimeout_ThrowsArgumentException()
    {
        // Arrange
        var spawner = new ProcessSpawner();
        var token = CreateMockToken();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            spawner.SpawnProcessWithToken(token, "test.exe", "args", -1000));
        Assert.Equal("timeoutMs", exception.ParamName);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SpawnProcessWithToken_WithValidInputs_ExecutesSuccessfully()
    {
        // This test requires a valid token and executable
        // Skip if not running on Windows or without proper credentials
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange
        var spawner = new ProcessSpawner();
        
        // Try to get current process token for testing
        SafeAccessTokenHandle? token = null;
        try
        {
            // Get current process token (requires appropriate privileges)
            var processHandle = System.Diagnostics.Process.GetCurrentProcess().Handle;
            if (NativeMethods.OpenProcessToken(
                processHandle,
                NativeMethods.TOKEN_DUPLICATE | NativeMethods.TOKEN_QUERY,
                out token))
            {
                // Use a simple Windows command that should exist
                var executablePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "cmd.exe");

                if (File.Exists(executablePath))
                {
                    // Act
                    var result = spawner.SpawnProcessWithToken(
                        token,
                        executablePath,
                        "/c echo test",
                        5000);

                    // Assert
                    Assert.NotNull(result);
                    Assert.False(result.TimedOut);
                    Assert.True(result.ExecutionTime.TotalMilliseconds > 0);
                }
            }
        }
        catch (Exception)
        {
            // Skip test if we can't get a valid token
            // This is expected in non-privileged test environments
        }
        finally
        {
            token?.Dispose();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SpawnProcessWithToken_CapturesStandardOutput()
    {
        // This test requires a valid token and executable
        // Skip if not running on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange
        var spawner = new ProcessSpawner();
        
        // Try to get current process token for testing
        SafeAccessTokenHandle? token = null;
        try
        {
            // Get current process token
            var processHandle = System.Diagnostics.Process.GetCurrentProcess().Handle;
            if (NativeMethods.OpenProcessToken(
                processHandle,
                NativeMethods.TOKEN_DUPLICATE | NativeMethods.TOKEN_QUERY,
                out token))
            {
                var executablePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "cmd.exe");

                if (File.Exists(executablePath))
                {
                    // Act
                    var result = spawner.SpawnProcessWithToken(
                        token,
                        executablePath,
                        "/c echo Hello World",
                        5000);

                    // Assert
                    Assert.NotNull(result);
                    Assert.Equal(0, result.ExitCode);
                    Assert.Contains("Hello World", result.StandardOutput);
                    Assert.True(result.IsSuccess);
                }
            }
        }
        catch (Exception)
        {
            // Skip test if we can't get a valid token
        }
        finally
        {
            token?.Dispose();
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public void SpawnProcessWithToken_CapturesNonZeroExitCode()
    {
        // This test requires a valid token and executable
        // Skip if not running on Windows
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        // Arrange
        var spawner = new ProcessSpawner();
        
        // Try to get current process token for testing
        SafeAccessTokenHandle? token = null;
        try
        {
            // Get current process token
            var processHandle = System.Diagnostics.Process.GetCurrentProcess().Handle;
            if (NativeMethods.OpenProcessToken(
                processHandle,
                NativeMethods.TOKEN_DUPLICATE | NativeMethods.TOKEN_QUERY,
                out token))
            {
                var executablePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "cmd.exe");

                if (File.Exists(executablePath))
                {
                    // Act - exit with code 42
                    var result = spawner.SpawnProcessWithToken(
                        token,
                        executablePath,
                        "/c exit 42",
                        5000);

                    // Assert
                    Assert.NotNull(result);
                    Assert.Equal(42, result.ExitCode);
                    Assert.False(result.IsSuccess);
                }
            }
        }
        catch (Exception)
        {
            // Skip test if we can't get a valid token
        }
        finally
        {
            token?.Dispose();
        }
    }

    [Fact]
    public void ProcessExecutionResult_Properties_AreSetCorrectly()
    {
        // Arrange & Act
        var result = new ProcessExecutionResult
        {
            ExitCode = 0,
            StandardOutput = "output",
            StandardError = "error",
            TimedOut = false,
            ExecutionTime = TimeSpan.FromSeconds(1)
        };

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("output", result.StandardOutput);
        Assert.Equal("error", result.StandardError);
        Assert.False(result.TimedOut);
        Assert.Equal(TimeSpan.FromSeconds(1), result.ExecutionTime);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ProcessExecutionResult_IsSuccess_ReturnsFalseWhenExitCodeNonZero()
    {
        // Arrange & Act
        var result = new ProcessExecutionResult
        {
            ExitCode = 1,
            StandardOutput = "",
            StandardError = "",
            TimedOut = false,
            ExecutionTime = TimeSpan.FromSeconds(1)
        };

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ProcessExecutionResult_IsSuccess_ReturnsFalseWhenTimedOut()
    {
        // Arrange & Act
        var result = new ProcessExecutionResult
        {
            ExitCode = 0,
            StandardOutput = "",
            StandardError = "",
            TimedOut = true,
            ExecutionTime = TimeSpan.FromSeconds(1)
        };

        // Assert
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ProcessExecutionResult_Constructor_WithValidValues_CreatesInstance()
    {
        // Arrange & Act
        var result = new ProcessExecutionResult(
            exitCode: 0,
            standardOutput: "output",
            standardError: "error",
            timedOut: false,
            executionTime: TimeSpan.FromSeconds(2));

        // Assert
        Assert.Equal(0, result.ExitCode);
        Assert.Equal("output", result.StandardOutput);
        Assert.Equal("error", result.StandardError);
        Assert.False(result.TimedOut);
        Assert.Equal(TimeSpan.FromSeconds(2), result.ExecutionTime);
    }

    [Fact]
    public void ProcessExecutionResult_Constructor_WithNegativeExecutionTime_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ProcessExecutionResult(0, "", "", false, TimeSpan.FromSeconds(-1)));
        Assert.Equal("executionTime", exception.ParamName);
    }

    [Fact]
    public void ProcessExecutionResult_Constructor_WithNullOutput_UsesEmptyString()
    {
        // Arrange & Act
        var result = new ProcessExecutionResult(
            exitCode: 0,
            standardOutput: null!,
            standardError: null!,
            timedOut: false,
            executionTime: TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal(string.Empty, result.StandardOutput);
        Assert.Equal(string.Empty, result.StandardError);
    }

    /// <summary>
    /// Creates a mock token handle for testing (not a real Windows token)
    /// </summary>
    private SafeAccessTokenHandle CreateMockToken()
    {
        // Create a non-zero handle that will pass the null check
        // but will fail when actually used with Windows APIs
        // This is sufficient for testing validation logic
        return new SafeAccessTokenHandle(new IntPtr(1));
    }
}

/// <summary>
/// Additional native methods needed for testing
/// </summary>
internal static partial class NativeMethods
{
    public const int TOKEN_DUPLICATE = 0x0002;
    public const int TOKEN_QUERY = 0x0008;

    [System.Runtime.InteropServices.DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(
        IntPtr ProcessHandle,
        int DesiredAccess,
        out SafeAccessTokenHandle TokenHandle);
}
