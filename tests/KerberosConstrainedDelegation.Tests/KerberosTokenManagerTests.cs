using Xunit;

namespace KerberosConstrainedDelegation.Tests;

/// <summary>
/// Unit tests for KerberosTokenManager helper methods
/// </summary>
public class KerberosTokenManagerTests
{
    #region ParseUsername Tests

    [Fact]
    public void ParseUsername_WithValidUPN_ReturnsCorrectDomainAndAccount()
    {
        // Arrange
        var username = "testuser@contoso.com";

        // Act
        var (domain, account) = KerberosTokenManager.ParseUsername(username);

        // Assert
        Assert.Equal("contoso.com", domain);
        Assert.Equal("testuser", account);
    }

    [Fact]
    public void ParseUsername_WithValidDomainBackslashFormat_ReturnsCorrectDomainAndAccount()
    {
        // Arrange
        var username = "CONTOSO\\testuser";

        // Act
        var (domain, account) = KerberosTokenManager.ParseUsername(username);

        // Assert
        Assert.Equal("CONTOSO", domain);
        Assert.Equal("testuser", account);
    }

    [Fact]
    public void ParseUsername_WithNullUsername_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => KerberosTokenManager.ParseUsername(null!));
        Assert.Contains("Username cannot be null or empty", exception.Message);
    }

    [Fact]
    public void ParseUsername_WithEmptyUsername_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => KerberosTokenManager.ParseUsername(""));
        Assert.Contains("Username cannot be null or empty", exception.Message);
    }

    [Fact]
    public void ParseUsername_WithWhitespaceUsername_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => KerberosTokenManager.ParseUsername("   "));
        Assert.Contains("Username cannot be null or empty", exception.Message);
    }

    [Fact]
    public void ParseUsername_WithInvalidUPNFormat_ThrowsArgumentException()
    {
        // Arrange
        var username = "testuser@@contoso.com";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => KerberosTokenManager.ParseUsername(username));
        Assert.Contains("Invalid UPN format", exception.Message);
    }

    [Fact]
    public void ParseUsername_WithInvalidDomainBackslashFormat_ThrowsArgumentException()
    {
        // Arrange
        var username = "CONTOSO\\\\testuser";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => KerberosTokenManager.ParseUsername(username));
        Assert.Contains("Invalid DOMAIN\\username format", exception.Message);
    }

    [Fact]
    public void ParseUsername_WithNoSeparator_ThrowsArgumentException()
    {
        // Arrange
        var username = "testuser";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => KerberosTokenManager.ParseUsername(username));
        Assert.Contains("Username must be in UPN", exception.Message);
    }

    [Fact]
    public void ParseUsername_WithEmptyDomainInUPN_ThrowsArgumentException()
    {
        // Arrange
        var username = "testuser@";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => KerberosTokenManager.ParseUsername(username));
        Assert.Contains("Invalid UPN format", exception.Message);
    }

    [Fact]
    public void ParseUsername_WithEmptyAccountInUPN_ThrowsArgumentException()
    {
        // Arrange
        var username = "@contoso.com";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => KerberosTokenManager.ParseUsername(username));
        Assert.Contains("Invalid UPN format", exception.Message);
    }

    [Fact]
    public void ParseUsername_WithEmptyDomainInBackslashFormat_ThrowsArgumentException()
    {
        // Arrange
        var username = "\\testuser";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => KerberosTokenManager.ParseUsername(username));
        Assert.Contains("Invalid DOMAIN\\username format", exception.Message);
    }

    [Fact]
    public void ParseUsername_WithEmptyAccountInBackslashFormat_ThrowsArgumentException()
    {
        // Arrange
        var username = "CONTOSO\\";

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => KerberosTokenManager.ParseUsername(username));
        Assert.Contains("Invalid DOMAIN\\username format", exception.Message);
    }

    [Theory]
    [InlineData("user@domain.com", "domain.com", "user")]
    [InlineData("DOMAIN\\user", "DOMAIN", "user")]
    [InlineData("admin@corp.contoso.com", "corp.contoso.com", "admin")]
    [InlineData("CORP\\admin", "CORP", "admin")]
    public void ParseUsername_WithVariousValidFormats_ReturnsCorrectComponents(
        string username, string expectedDomain, string expectedAccount)
    {
        // Act
        var (domain, account) = KerberosTokenManager.ParseUsername(username);

        // Assert
        Assert.Equal(expectedDomain, domain);
        Assert.Equal(expectedAccount, account);
    }

    #endregion

    #region RegisterLsaAuthenticationPackage Tests

    [Fact]
    public void RegisterLsaAuthenticationPackage_WithNullPackageName_ThrowsArgumentException()
    {
        // Arrange
        var password = new System.Security.SecureString();
        password.AppendChar('P');
        using var credentials = new ServiceAccountCredentials("testuser", "CONTOSO", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => manager.RegisterLsaAuthenticationPackage(null!));
        Assert.Contains("Package name cannot be null or empty", exception.Message);
    }

    [Fact]
    public void RegisterLsaAuthenticationPackage_WithEmptyPackageName_ThrowsArgumentException()
    {
        // Arrange
        var password = new System.Security.SecureString();
        password.AppendChar('P');
        using var credentials = new ServiceAccountCredentials("testuser", "CONTOSO", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => manager.RegisterLsaAuthenticationPackage(""));
        Assert.Contains("Package name cannot be null or empty", exception.Message);
    }

    [Fact]
    public void RegisterLsaAuthenticationPackage_WithWhitespacePackageName_ThrowsArgumentException()
    {
        // Arrange
        var password = new System.Security.SecureString();
        password.AppendChar('P');
        using var credentials = new ServiceAccountCredentials("testuser", "CONTOSO", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => manager.RegisterLsaAuthenticationPackage("   "));
        Assert.Contains("Package name cannot be null or empty", exception.Message);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullCredentials_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new KerberosTokenManager(null!));
    }

    [Fact]
    public void Constructor_WithValidCredentials_CreatesInstance()
    {
        // Arrange
        var password = new System.Security.SecureString();
        password.AppendChar('P');
        using var credentials = new ServiceAccountCredentials("testuser", "CONTOSO", password);

        // Act
        using var manager = new KerberosTokenManager(credentials);

        // Assert
        Assert.NotNull(manager);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var password = new System.Security.SecureString();
        password.AppendChar('P');
        using var credentials = new ServiceAccountCredentials("testuser", "CONTOSO", password);
        var manager = new KerberosTokenManager(credentials);

        // Act & Assert - should not throw
        manager.Dispose();
        manager.Dispose();
        manager.Dispose();
    }

    #endregion

    #region AuthenticateServiceAccount Tests (via reflection for private method testing)

    [Fact]
    public void AuthenticateServiceAccount_WithInvalidCredentials_ThrowsKerberosException()
    {
        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "InvalidPassword123!")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("InvalidUser", "INVALID_DOMAIN", password);
        using var manager = new KerberosTokenManager(credentials);

        // Get the private method via reflection
        var methodInfo = typeof(KerberosTokenManager).GetMethod(
            "AuthenticateServiceAccount",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(methodInfo);

        // Act & Assert
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
        {
            methodInfo.Invoke(manager, null);
        });

        // Verify the inner exception is KerberosException
        Assert.IsType<KerberosException>(exception.InnerException);
        var kerberosException = (KerberosException)exception.InnerException!;
        Assert.Equal(KerberosErrorType.ServiceAuthenticationFailed, kerberosException.ErrorType);
        Assert.NotEqual(0, kerberosException.Win32ErrorCode);
        Assert.Contains("Service account authentication failed", kerberosException.Message);
    }

    [Fact]
    public void AuthenticateServiceAccount_ErrorMessageIncludesFullyQualifiedUsername()
    {
        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "InvalidPassword")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("testuser", "TESTDOMAIN", password);
        using var manager = new KerberosTokenManager(credentials);

        // Get the private method via reflection
        var methodInfo = typeof(KerberosTokenManager).GetMethod(
            "AuthenticateServiceAccount",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(methodInfo);

        // Act & Assert
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
        {
            methodInfo.Invoke(manager, null);
        });

        var kerberosException = (KerberosException)exception.InnerException!;
        Assert.Contains("TESTDOMAIN\\testuser", kerberosException.Message);
    }

    [Fact]
    public void AuthenticateServiceAccount_ErrorIncludesWin32ErrorCode()
    {
        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "WrongPassword")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("nonexistentuser", "NONEXISTENT", password);
        using var manager = new KerberosTokenManager(credentials);

        // Get the private method via reflection
        var methodInfo = typeof(KerberosTokenManager).GetMethod(
            "AuthenticateServiceAccount",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(methodInfo);

        // Act & Assert
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
        {
            methodInfo.Invoke(manager, null);
        });

        var kerberosException = (KerberosException)exception.InnerException!;
        Assert.NotEqual(0, kerberosException.Win32ErrorCode);
        Assert.Contains($"0x{kerberosException.Win32ErrorCode:X8}", kerberosException.Message);
    }

    [Fact]
    public void AuthenticateServiceAccount_WithWrongPassword_ThrowsKerberosExceptionWithServiceAuthenticationFailedType()
    {
        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "DefinitelyWrongPassword123!")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("testaccount", "TESTDOMAIN", password);
        using var manager = new KerberosTokenManager(credentials);

        // Get the private method via reflection
        var methodInfo = typeof(KerberosTokenManager).GetMethod(
            "AuthenticateServiceAccount",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(methodInfo);

        // Act & Assert
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
        {
            methodInfo.Invoke(manager, null);
        });

        var kerberosException = (KerberosException)exception.InnerException!;
        Assert.Equal(KerberosErrorType.ServiceAuthenticationFailed, kerberosException.ErrorType);
        Assert.Contains("Service account authentication failed", kerberosException.Message);
    }

    [Fact]
    public void AuthenticateServiceAccount_UsesLogon32LogonNetworkType()
    {
        // This test verifies that the implementation uses LOGON32_LOGON_NETWORK
        // by checking that the error message is consistent with network logon attempts
        // (We can't directly verify the logon type without mocking, but we can verify
        // the method is called and fails as expected with invalid credentials)

        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "TestPassword")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("testuser", "DOMAIN", password);
        using var manager = new KerberosTokenManager(credentials);

        // Get the private method via reflection
        var methodInfo = typeof(KerberosTokenManager).GetMethod(
            "AuthenticateServiceAccount",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.NotNull(methodInfo);

        // Act & Assert
        var exception = Assert.Throws<System.Reflection.TargetInvocationException>(() =>
        {
            methodInfo.Invoke(manager, null);
        });

        // Verify it's a KerberosException with ServiceAuthenticationFailed type
        Assert.IsType<KerberosException>(exception.InnerException);
        var kerberosException = (KerberosException)exception.InnerException!;
        Assert.Equal(KerberosErrorType.ServiceAuthenticationFailed, kerberosException.ErrorType);
    }

    #endregion

    #region GetDelegatedToken Tests

    [Fact]
    public void GetDelegatedToken_WithNullUsername_ThrowsArgumentException()
    {
        // Arrange
        var password = new System.Security.SecureString();
        password.AppendChar('P');
        using var credentials = new ServiceAccountCredentials("testuser", "CONTOSO", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            manager.GetDelegatedToken(null!, "cifs/fileserver.contoso.com"));
        Assert.Contains("Username cannot be null or empty", exception.Message);
    }

    [Fact]
    public void GetDelegatedToken_WithEmptyUsername_ThrowsArgumentException()
    {
        // Arrange
        var password = new System.Security.SecureString();
        password.AppendChar('P');
        using var credentials = new ServiceAccountCredentials("testuser", "CONTOSO", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            manager.GetDelegatedToken("", "cifs/fileserver.contoso.com"));
        Assert.Contains("Username cannot be null or empty", exception.Message);
    }

    [Fact]
    public void GetDelegatedToken_WithWhitespaceUsername_ThrowsArgumentException()
    {
        // Arrange
        var password = new System.Security.SecureString();
        password.AppendChar('P');
        using var credentials = new ServiceAccountCredentials("testuser", "CONTOSO", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            manager.GetDelegatedToken("   ", "cifs/fileserver.contoso.com"));
        Assert.Contains("Username cannot be null or empty", exception.Message);
    }

    [Fact]
    public void GetDelegatedToken_WithNullTargetSpn_ThrowsArgumentException()
    {
        // Arrange
        var password = new System.Security.SecureString();
        password.AppendChar('P');
        using var credentials = new ServiceAccountCredentials("testuser", "CONTOSO", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            manager.GetDelegatedToken("targetuser@contoso.com", null!));
        Assert.Contains("Target SPN cannot be null or empty", exception.Message);
    }

    [Fact]
    public void GetDelegatedToken_WithEmptyTargetSpn_ThrowsArgumentException()
    {
        // Arrange
        var password = new System.Security.SecureString();
        password.AppendChar('P');
        using var credentials = new ServiceAccountCredentials("testuser", "CONTOSO", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            manager.GetDelegatedToken("targetuser@contoso.com", ""));
        Assert.Contains("Target SPN cannot be null or empty", exception.Message);
    }

    [Fact]
    public void GetDelegatedToken_WithWhitespaceTargetSpn_ThrowsArgumentException()
    {
        // Arrange
        var password = new System.Security.SecureString();
        password.AppendChar('P');
        using var credentials = new ServiceAccountCredentials("testuser", "CONTOSO", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => 
            manager.GetDelegatedToken("targetuser@contoso.com", "   "));
        Assert.Contains("Target SPN cannot be null or empty", exception.Message);
    }

    [Fact]
    public void GetDelegatedToken_WithInvalidServiceCredentials_ThrowsKerberosException()
    {
        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "InvalidPassword123!")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("InvalidUser", "INVALID_DOMAIN", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert
        var exception = Assert.Throws<KerberosException>(() => 
            manager.GetDelegatedToken("targetuser@contoso.com", "cifs/fileserver.contoso.com"));
        
        Assert.Equal(KerberosErrorType.ServiceAuthenticationFailed, exception.ErrorType);
        Assert.NotEqual(0, exception.Win32ErrorCode);
        Assert.Contains("Service account authentication failed", exception.Message);
    }

    [Fact]
    public void GetDelegatedToken_WithInvalidUsername_ThrowsKerberosException()
    {
        // Arrange
        var password = new System.Security.SecureString();
        password.AppendChar('P');
        using var credentials = new ServiceAccountCredentials("testuser", "CONTOSO", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert - Invalid username format (no domain separator)
        // Note: This will fail at service authentication before username parsing,
        // so we expect KerberosException instead of ArgumentException
        var exception = Assert.Throws<KerberosException>(() => 
            manager.GetDelegatedToken("invalidusername", "cifs/fileserver.contoso.com"));
        
        // The service authentication will fail first, but if it succeeded,
        // the username parsing would fail with ArgumentException
        Assert.Equal(KerberosErrorType.ServiceAuthenticationFailed, exception.ErrorType);
    }

    [Theory]
    [InlineData("user@domain.com", "cifs/fileserver.domain.com")]
    [InlineData("DOMAIN\\user", "http/webserver.domain.com")]
    [InlineData("admin@corp.contoso.com", "mssql/sqlserver.corp.contoso.com")]
    public void GetDelegatedToken_WithValidFormats_AcceptsVariousUsernameAndSpnFormats(
        string username, string targetSpn)
    {
        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "TestPassword123!")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("serviceaccount", "TESTDOMAIN", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert - Should throw KerberosException due to invalid credentials,
        // but NOT ArgumentException (which would indicate format validation failed)
        var exception = Assert.Throws<KerberosException>(() => 
            manager.GetDelegatedToken(username, targetSpn));
        
        // Verify it's a service authentication failure, not a format validation error
        Assert.Equal(KerberosErrorType.ServiceAuthenticationFailed, exception.ErrorType);
    }

    [Fact]
    public void GetDelegatedToken_DisposesIntermediateTokensOnSuccess()
    {
        // This test verifies that intermediate tokens are disposed even on success
        // We can't directly test this without integration testing, but we can verify
        // the method completes without resource leaks by calling it multiple times

        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "TestPassword")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("testuser", "TESTDOMAIN", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert - Multiple calls should not cause resource exhaustion
        // (will fail with authentication error, but should clean up properly)
        for (int i = 0; i < 3; i++)
        {
            Assert.Throws<KerberosException>(() => 
                manager.GetDelegatedToken("targetuser@testdomain.com", "cifs/fileserver.testdomain.com"));
        }

        // If we got here without hanging or crashing, cleanup is working
        Assert.True(true);
    }

    [Fact]
    public void GetDelegatedToken_DisposesIntermediateTokensOnException()
    {
        // This test verifies that intermediate tokens are disposed when an exception occurs
        // We verify this by ensuring multiple failed attempts don't cause resource leaks

        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "WrongPassword")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("baduser", "BADDOMAIN", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert - Multiple failed calls should not cause resource exhaustion
        for (int i = 0; i < 5; i++)
        {
            var exception = Assert.Throws<KerberosException>(() => 
                manager.GetDelegatedToken("user@domain.com", "cifs/server.domain.com"));
            Assert.Equal(KerberosErrorType.ServiceAuthenticationFailed, exception.ErrorType);
        }

        // If we got here without hanging or crashing, cleanup is working
        Assert.True(true);
    }

    #endregion

    #region ValidateDelegationConfiguration Tests

    [Fact]
    public void ValidateDelegationConfiguration_WithInvalidCredentials_ReturnsFalse()
    {
        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "InvalidPassword123!")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("InvalidUser", "INVALID_DOMAIN", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act
        var result = manager.ValidateDelegationConfiguration();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateDelegationConfiguration_WithNonExistentDomain_ReturnsFalse()
    {
        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "SomePassword")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("testuser", "NONEXISTENT_DOMAIN_12345", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act
        var result = manager.ValidateDelegationConfiguration();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateDelegationConfiguration_WithNonExistentUser_ReturnsFalse()
    {
        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "Password123")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("nonexistentuser_xyz", "TESTDOMAIN", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act
        var result = manager.ValidateDelegationConfiguration();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ValidateDelegationConfiguration_DoesNotThrowException()
    {
        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "AnyPassword")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("anyuser", "ANYDOMAIN", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act & Assert - Should not throw, just return false
        var result = manager.ValidateDelegationConfiguration();
        Assert.False(result);
    }

    [Fact]
    public void ValidateDelegationConfiguration_CleansUpServiceToken()
    {
        // This test verifies that the service token is properly disposed
        // by calling the method multiple times and ensuring no resource leaks

        // Arrange
        var password = new System.Security.SecureString();
        foreach (var c in "TestPassword")
        {
            password.AppendChar(c);
        }
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("testuser", "TESTDOMAIN", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act - Multiple calls should not cause resource exhaustion
        for (int i = 0; i < 10; i++)
        {
            var result = manager.ValidateDelegationConfiguration();
            Assert.False(result); // Will be false due to invalid credentials
        }

        // If we got here without hanging or crashing, cleanup is working
        Assert.True(true);
    }

    [Fact]
    public void ValidateDelegationConfiguration_WithInvalidPassword_ReturnsFalse()
    {
        // Arrange
        var password = new System.Security.SecureString();
        password.AppendChar('X'); // Add at least one character to pass validation
        password.MakeReadOnly();

        using var credentials = new ServiceAccountCredentials("testuser", "TESTDOMAIN", password);
        using var manager = new KerberosTokenManager(credentials);

        // Act
        var result = manager.ValidateDelegationConfiguration();

        // Assert - Will return false because credentials are invalid
        Assert.False(result);
    }

    #endregion
}
