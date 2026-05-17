using Microsoft.Extensions.Configuration;
using Xunit;

namespace KerberosConstrainedDelegation.Tests;

/// <summary>
/// Unit tests for ConfigurationManager class
/// </summary>
public class ConfigurationManagerTests
{
    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new ConfigurationManager(null!));
    }

    [Fact]
    public void GetServiceCredentials_WithValidConfiguration_ReturnsCredentials()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ServiceAccount:Username"] = "testuser",
            ["ServiceAccount:Domain"] = "TESTDOMAIN",
            ["ServiceAccount:Password"] = "password123"
        });
        using var manager = new ConfigurationManager(config);

        // Act
        var credentials = manager.GetServiceCredentials();

        // Assert
        Assert.NotNull(credentials);
        Assert.Equal("testuser", credentials.Username);
        Assert.Equal("TESTDOMAIN", credentials.Domain);
        Assert.NotNull(credentials.Password);
        Assert.Equal("TESTDOMAIN\\testuser", credentials.FullyQualifiedUsername);
    }

    [Fact]
    public void GetServiceCredentials_WithMissingUsername_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ServiceAccount:Domain"] = "TESTDOMAIN",
            ["ServiceAccount:Password"] = "password123"
        });
        using var manager = new ConfigurationManager(config);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => manager.GetServiceCredentials());
        Assert.Contains("ServiceAccount:Username", exception.Message);
    }

    [Fact]
    public void GetServiceCredentials_WithMissingDomain_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ServiceAccount:Username"] = "testuser",
            ["ServiceAccount:Password"] = "password123"
        });
        using var manager = new ConfigurationManager(config);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => manager.GetServiceCredentials());
        Assert.Contains("ServiceAccount:Domain", exception.Message);
    }

    [Fact]
    public void GetServiceCredentials_WithMissingPassword_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ServiceAccount:Username"] = "testuser",
            ["ServiceAccount:Domain"] = "TESTDOMAIN"
        });
        using var manager = new ConfigurationManager(config);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => manager.GetServiceCredentials());
        Assert.Contains("ServiceAccount:Password", exception.Message);
    }

    [Fact]
    public void GetTargetUsername_WithValidConfiguration_ReturnsUsername()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["TargetUser:Username"] = "user@contoso.com"
        });
        using var manager = new ConfigurationManager(config);

        // Act
        var username = manager.GetTargetUsername();

        // Assert
        Assert.Equal("user@contoso.com", username);
    }

    [Fact]
    public void GetTargetUsername_WithMissingUsername_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>());
        using var manager = new ConfigurationManager(config);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => manager.GetTargetUsername());
        Assert.Contains("TargetUser:Username", exception.Message);
    }

    [Fact]
    public void GetTargetServicePrincipalName_WithValidConfiguration_ReturnsSpn()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["TargetService:Spn"] = "cifs/fileserver.contoso.com"
        });
        using var manager = new ConfigurationManager(config);

        // Act
        var spn = manager.GetTargetServicePrincipalName();

        // Assert
        Assert.Equal("cifs/fileserver.contoso.com", spn);
    }

    [Fact]
    public void GetTargetServicePrincipalName_WithMissingSpn_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>());
        using var manager = new ConfigurationManager(config);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => manager.GetTargetServicePrincipalName());
        Assert.Contains("TargetService:Spn", exception.Message);
    }

    [Fact]
    public void GetExternalExecutablePath_WithValidConfiguration_ReturnsPath()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ExternalExecutable:Path"] = "C:\\Tools\\FileShareWriter.exe"
        });
        using var manager = new ConfigurationManager(config);

        // Act
        var path = manager.GetExternalExecutablePath();

        // Assert
        Assert.Equal("C:\\Tools\\FileShareWriter.exe", path);
    }

    [Fact]
    public void GetExternalExecutablePath_WithMissingPath_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>());
        using var manager = new ConfigurationManager(config);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => manager.GetExternalExecutablePath());
        Assert.Contains("ExternalExecutable:Path", exception.Message);
    }

    [Fact]
    public void GetFileSharePath_WithValidConfiguration_ReturnsPath()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["FileShare:Path"] = "\\\\fileserver\\share\\test.txt"
        });
        using var manager = new ConfigurationManager(config);

        // Act
        var path = manager.GetFileSharePath();

        // Assert
        Assert.Equal("\\\\fileserver\\share\\test.txt", path);
    }

    [Fact]
    public void GetFileSharePath_WithMissingPath_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>());
        using var manager = new ConfigurationManager(config);

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() => manager.GetFileSharePath());
        Assert.Contains("FileShare:Path", exception.Message);
    }

    [Fact]
    public void GetTimeoutSeconds_WithValidConfiguration_ReturnsTimeout()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Execution:TimeoutSeconds"] = "60"
        });
        using var manager = new ConfigurationManager(config);

        // Act
        var timeout = manager.GetTimeoutSeconds();

        // Assert
        Assert.Equal(60, timeout);
    }

    [Fact]
    public void GetTimeoutSeconds_WithMissingConfiguration_ReturnsDefaultTimeout()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>());
        using var manager = new ConfigurationManager(config);

        // Act
        var timeout = manager.GetTimeoutSeconds();

        // Assert
        Assert.Equal(30, timeout); // Default timeout
    }

    [Fact]
    public void GetTimeoutSeconds_WithInvalidValue_ReturnsDefaultTimeout()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Execution:TimeoutSeconds"] = "invalid"
        });
        using var manager = new ConfigurationManager(config);

        // Act
        var timeout = manager.GetTimeoutSeconds();

        // Assert
        Assert.Equal(30, timeout); // Default timeout
    }

    [Fact]
    public void GetTimeoutSeconds_WithNegativeValue_ReturnsDefaultTimeout()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["Execution:TimeoutSeconds"] = "-10"
        });
        using var manager = new ConfigurationManager(config);

        // Act
        var timeout = manager.GetTimeoutSeconds();

        // Assert
        Assert.Equal(30, timeout); // Default timeout
    }

    [Fact]
    public void ValidateConfiguration_WithValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var config = CreateConfiguration(new Dictionary<string, string?>
            {
                ["ServiceAccount:Username"] = "testuser",
                ["ServiceAccount:Domain"] = "TESTDOMAIN",
                ["ServiceAccount:Password"] = "password123",
                ["TargetUser:Username"] = "user@contoso.com",
                ["TargetService:Spn"] = "cifs/fileserver.contoso.com",
                ["ExternalExecutable:Path"] = tempFile,
                ["FileShare:Path"] = "\\\\fileserver\\share\\test.txt",
                ["Execution:TimeoutSeconds"] = "30"
            });
            using var manager = new ConfigurationManager(config);

            // Act
            var result = manager.ValidateConfiguration();

            // Assert
            Assert.True(result.IsValid);
            Assert.Empty(result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ValidateConfiguration_WithMissingServiceUsername_ReturnsFailure()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ServiceAccount:Domain"] = "TESTDOMAIN",
            ["ServiceAccount:Password"] = "password123"
        });
        using var manager = new ConfigurationManager(config);

        // Act
        var result = manager.ValidateConfiguration();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("ServiceAccount:Username", result.ErrorMessage);
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidServiceUsername_ReturnsFailure()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ServiceAccount:Username"] = "user\\name",
            ["ServiceAccount:Domain"] = "TESTDOMAIN",
            ["ServiceAccount:Password"] = "password123"
        });
        using var manager = new ConfigurationManager(config);

        // Act
        var result = manager.ValidateConfiguration();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("invalid characters", result.ErrorMessage);
    }

    [Theory]
    [InlineData("user@contoso.com")]
    [InlineData("CONTOSO\\user")]
    public void ValidateConfiguration_WithValidUsernameFormats_ReturnsSuccess(string username)
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var config = CreateConfiguration(new Dictionary<string, string?>
            {
                ["ServiceAccount:Username"] = "testuser",
                ["ServiceAccount:Domain"] = "TESTDOMAIN",
                ["ServiceAccount:Password"] = "password123",
                ["TargetUser:Username"] = username,
                ["TargetService:Spn"] = "cifs/fileserver.contoso.com",
                ["ExternalExecutable:Path"] = tempFile,
                ["FileShare:Path"] = "\\\\fileserver\\share\\test.txt"
            });
            using var manager = new ConfigurationManager(config);

            // Act
            var result = manager.ValidateConfiguration();

            // Assert
            Assert.True(result.IsValid);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("invalidusername")]
    [InlineData("user")]
    [InlineData("@domain.com")]
    [InlineData("\\username")]
    public void ValidateConfiguration_WithInvalidUsernameFormat_ReturnsFailure(string username)
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ServiceAccount:Username"] = "testuser",
            ["ServiceAccount:Domain"] = "TESTDOMAIN",
            ["ServiceAccount:Password"] = "password123",
            ["TargetUser:Username"] = username,
            ["TargetService:Spn"] = "cifs/fileserver.contoso.com",
            ["ExternalExecutable:Path"] = "C:\\Tools\\FileShareWriter.exe",
            ["FileShare:Path"] = "\\\\fileserver\\share\\test.txt"
        });
        using var manager = new ConfigurationManager(config);

        // Act
        var result = manager.ValidateConfiguration();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("UPN format", result.ErrorMessage);
    }

    [Theory]
    [InlineData("cifs/fileserver.contoso.com")]
    [InlineData("http/webapp.contoso.com")]
    [InlineData("mssql/sqlserver.contoso.com:1433")]
    [InlineData("ldap/dc.contoso.com:389")]
    public void ValidateConfiguration_WithValidSpnFormats_ReturnsSuccess(string spn)
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var config = CreateConfiguration(new Dictionary<string, string?>
            {
                ["ServiceAccount:Username"] = "testuser",
                ["ServiceAccount:Domain"] = "TESTDOMAIN",
                ["ServiceAccount:Password"] = "password123",
                ["TargetUser:Username"] = "user@contoso.com",
                ["TargetService:Spn"] = spn,
                ["ExternalExecutable:Path"] = tempFile,
                ["FileShare:Path"] = "\\\\fileserver\\share\\test.txt"
            });
            using var manager = new ConfigurationManager(config);

            // Act
            var result = manager.ValidateConfiguration();

            // Assert
            Assert.True(result.IsValid);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("invalidspn")]
    [InlineData("fileserver.contoso.com")]
    [InlineData("cifs\\fileserver.contoso.com")]
    [InlineData("cifs/")]
    [InlineData("/fileserver.contoso.com")]
    public void ValidateConfiguration_WithInvalidSpnFormat_ReturnsFailure(string spn)
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ServiceAccount:Username"] = "testuser",
            ["ServiceAccount:Domain"] = "TESTDOMAIN",
            ["ServiceAccount:Password"] = "password123",
            ["TargetUser:Username"] = "user@contoso.com",
            ["TargetService:Spn"] = spn,
            ["ExternalExecutable:Path"] = "C:\\Tools\\FileShareWriter.exe",
            ["FileShare:Path"] = "\\\\fileserver\\share\\test.txt"
        });
        using var manager = new ConfigurationManager(config);

        // Act
        var result = manager.ValidateConfiguration();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("service/host.domain.com", result.ErrorMessage);
    }

    [Fact]
    public void ValidateConfiguration_WithNonExistentExecutable_ReturnsFailure()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ServiceAccount:Username"] = "testuser",
            ["ServiceAccount:Domain"] = "TESTDOMAIN",
            ["ServiceAccount:Password"] = "password123",
            ["TargetUser:Username"] = "user@contoso.com",
            ["TargetService:Spn"] = "cifs/fileserver.contoso.com",
            ["ExternalExecutable:Path"] = "C:\\NonExistent\\FileShareWriter.exe",
            ["FileShare:Path"] = "\\\\fileserver\\share\\test.txt"
        });
        using var manager = new ConfigurationManager(config);

        // Act
        var result = manager.ValidateConfiguration();

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("not found", result.ErrorMessage);
    }

    [Theory]
    [InlineData("\\\\fileserver\\share\\test.txt")]
    [InlineData("\\\\server\\share\\folder\\subfolder\\file.txt")]
    [InlineData("\\\\192.168.1.100\\share\\file.txt")]
    public void ValidateConfiguration_WithValidUncPaths_ReturnsSuccess(string uncPath)
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var config = CreateConfiguration(new Dictionary<string, string?>
            {
                ["ServiceAccount:Username"] = "testuser",
                ["ServiceAccount:Domain"] = "TESTDOMAIN",
                ["ServiceAccount:Password"] = "password123",
                ["TargetUser:Username"] = "user@contoso.com",
                ["TargetService:Spn"] = "cifs/fileserver.contoso.com",
                ["ExternalExecutable:Path"] = tempFile,
                ["FileShare:Path"] = uncPath
            });
            using var manager = new ConfigurationManager(config);

            // Act
            var result = manager.ValidateConfiguration();

            // Assert
            Assert.True(result.IsValid);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Theory]
    [InlineData("C:\\local\\path\\file.txt")]
    [InlineData("\\fileserver\\share\\test.txt")]
    [InlineData("fileserver\\share\\test.txt")]
    [InlineData("\\\\")]
    [InlineData("\\\\server")]
    public void ValidateConfiguration_WithInvalidUncPaths_ReturnsFailure(string uncPath)
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var config = CreateConfiguration(new Dictionary<string, string?>
            {
                ["ServiceAccount:Username"] = "testuser",
                ["ServiceAccount:Domain"] = "TESTDOMAIN",
                ["ServiceAccount:Password"] = "password123",
                ["TargetUser:Username"] = "user@contoso.com",
                ["TargetService:Spn"] = "cifs/fileserver.contoso.com",
                ["ExternalExecutable:Path"] = tempFile,
                ["FileShare:Path"] = uncPath
            });
            using var manager = new ConfigurationManager(config);

            // Act
            var result = manager.ValidateConfiguration();

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("UNC path", result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void ValidateConfiguration_WithInvalidTimeout_ReturnsFailure()
    {
        // Arrange
        var tempFile = Path.GetTempFileName();
        try
        {
            var config = CreateConfiguration(new Dictionary<string, string?>
            {
                ["ServiceAccount:Username"] = "testuser",
                ["ServiceAccount:Domain"] = "TESTDOMAIN",
                ["ServiceAccount:Password"] = "password123",
                ["TargetUser:Username"] = "user@contoso.com",
                ["TargetService:Spn"] = "cifs/fileserver.contoso.com",
                ["ExternalExecutable:Path"] = tempFile,
                ["FileShare:Path"] = "\\\\fileserver\\share\\test.txt",
                ["Execution:TimeoutSeconds"] = "-10"
            });
            using var manager = new ConfigurationManager(config);

            // Act
            var result = manager.ValidateConfiguration();

            // Assert
            Assert.False(result.IsValid);
            Assert.Contains("positive integer", result.ErrorMessage);
        }
        finally
        {
            if (File.Exists(tempFile))
                File.Delete(tempFile);
        }
    }

    [Fact]
    public void Dispose_DisposesServiceCredentials()
    {
        // Arrange
        var config = CreateConfiguration(new Dictionary<string, string?>
        {
            ["ServiceAccount:Username"] = "testuser",
            ["ServiceAccount:Domain"] = "TESTDOMAIN",
            ["ServiceAccount:Password"] = "password123"
        });
        var manager = new ConfigurationManager(config);
        
        // Get credentials to ensure they are created
        var credentials = manager.GetServiceCredentials();

        // Act
        manager.Dispose();

        // Assert - No exception should be thrown
        // Multiple dispose calls should be safe
        manager.Dispose();
    }

    /// <summary>
    /// Helper method to create an IConfiguration from a dictionary
    /// </summary>
    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
