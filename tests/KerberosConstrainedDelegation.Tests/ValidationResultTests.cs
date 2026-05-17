using Xunit;

namespace KerberosConstrainedDelegation.Tests;

/// <summary>
/// Unit tests for ValidationResult class
/// </summary>
public class ValidationResultTests
{
    [Fact]
    public void Success_CreatesValidResult()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal(string.Empty, result.ErrorMessage);
    }

    [Fact]
    public void Failure_WithErrorMessage_CreatesInvalidResult()
    {
        // Arrange
        var errorMessage = "Configuration is invalid";

        // Act
        var result = ValidationResult.Failure(errorMessage);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(errorMessage, result.ErrorMessage);
    }

    [Fact]
    public void Failure_WithNullErrorMessage_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ValidationResult.Failure(null!));
    }

    [Fact]
    public void Failure_WithEmptyErrorMessage_CreatesInvalidResult()
    {
        // Act
        var result = ValidationResult.Failure(string.Empty);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal(string.Empty, result.ErrorMessage);
    }

    [Fact]
    public void Success_AlwaysReturnsEmptyErrorMessage()
    {
        // Act
        var result = ValidationResult.Success();

        // Assert
        Assert.NotNull(result.ErrorMessage);
        Assert.Empty(result.ErrorMessage);
    }

    [Theory]
    [InlineData("Missing username")]
    [InlineData("Invalid format")]
    [InlineData("File not found")]
    public void Failure_PreservesErrorMessage(string errorMessage)
    {
        // Act
        var result = ValidationResult.Failure(errorMessage);

        // Assert
        Assert.Equal(errorMessage, result.ErrorMessage);
    }
}
