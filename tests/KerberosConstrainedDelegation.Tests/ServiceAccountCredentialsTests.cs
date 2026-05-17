using System.Security;
using Xunit;

namespace KerberosConstrainedDelegation.Tests;

/// <summary>
/// Unit tests for ServiceAccountCredentials class
/// </summary>
public class ServiceAccountCredentialsTests
{
    [Fact]
    public void Constructor_WithValidInputs_CreatesInstance()
    {
        // Arrange
        var username = "testuser";
        var domain = "TESTDOMAIN";
        var password = CreateSecureString("password123");

        // Act
        using var credentials = new ServiceAccountCredentials(username, domain, password);

        // Assert
        Assert.Equal(username, credentials.Username);
        Assert.Equal(domain, credentials.Domain);
        Assert.NotNull(credentials.Password);
        Assert.Equal("TESTDOMAIN\\testuser", credentials.FullyQualifiedUsername);
    }

    [Fact]
    public void Constructor_WithNullUsername_ThrowsArgumentException()
    {
        // Arrange
        var password = CreateSecureString("password123");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ServiceAccountCredentials(null!, "DOMAIN", password));
        Assert.Equal("username", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithEmptyUsername_ThrowsArgumentException()
    {
        // Arrange
        var password = CreateSecureString("password123");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ServiceAccountCredentials("", "DOMAIN", password));
        Assert.Equal("username", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithWhitespaceUsername_ThrowsArgumentException()
    {
        // Arrange
        var password = CreateSecureString("password123");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ServiceAccountCredentials("   ", "DOMAIN", password));
        Assert.Equal("username", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullDomain_ThrowsArgumentException()
    {
        // Arrange
        var password = CreateSecureString("password123");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ServiceAccountCredentials("testuser", null!, password));
        Assert.Equal("domain", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithEmptyDomain_ThrowsArgumentException()
    {
        // Arrange
        var password = CreateSecureString("password123");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ServiceAccountCredentials("testuser", "", password));
        Assert.Equal("domain", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithNullPassword_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ServiceAccountCredentials("testuser", "DOMAIN", null!));
        Assert.Equal("password", exception.ParamName);
    }

    [Fact]
    public void Constructor_WithEmptyPassword_ThrowsArgumentException()
    {
        // Arrange
        var password = new SecureString();
        password.MakeReadOnly();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ServiceAccountCredentials("testuser", "DOMAIN", password));
        Assert.Equal("password", exception.ParamName);
    }

    [Theory]
    [InlineData("user\\name")]
    [InlineData("user/name")]
    [InlineData("user:name")]
    [InlineData("user*name")]
    [InlineData("user?name")]
    [InlineData("user\"name")]
    [InlineData("user<name")]
    [InlineData("user>name")]
    [InlineData("user|name")]
    public void Constructor_WithInvalidCharactersInUsername_ThrowsArgumentException(string username)
    {
        // Arrange
        var password = CreateSecureString("password123");

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new ServiceAccountCredentials(username, "DOMAIN", password));
        Assert.Equal("username", exception.ParamName);
        Assert.Contains("invalid characters", exception.Message);
    }

    [Fact]
    public void FullyQualifiedUsername_ReturnsCorrectFormat()
    {
        // Arrange
        var password = CreateSecureString("password123");
        using var credentials = new ServiceAccountCredentials("testuser", "CONTOSO", password);

        // Act
        var fullyQualified = credentials.FullyQualifiedUsername;

        // Assert
        Assert.Equal("CONTOSO\\testuser", fullyQualified);
    }

    [Fact]
    public void Dispose_DisposesPasswordSecurely()
    {
        // Arrange
        var password = CreateSecureString("password123");
        var credentials = new ServiceAccountCredentials("testuser", "DOMAIN", password);

        // Act
        credentials.Dispose();

        // Assert - No exception should be thrown
        // Multiple dispose calls should be safe
        credentials.Dispose();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var password = CreateSecureString("password123");
        var credentials = new ServiceAccountCredentials("testuser", "DOMAIN", password);

        // Act & Assert - Should not throw
        credentials.Dispose();
        credentials.Dispose();
        credentials.Dispose();
    }

    /// <summary>
    /// Helper method to create a SecureString from a plain text password
    /// </summary>
    private static SecureString CreateSecureString(string password)
    {
        var securePassword = new SecureString();
        foreach (char c in password)
        {
            securePassword.AppendChar(c);
        }
        securePassword.MakeReadOnly();
        return securePassword;
    }
}
