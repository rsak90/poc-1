namespace KerberosConstrainedDelegation;

/// <summary>
/// Represents the result of a validation operation
/// </summary>
public sealed class ValidationResult
{
    /// <summary>
    /// Gets a value indicating whether the validation was successful
    /// </summary>
    public bool IsValid { get; init; }

    /// <summary>
    /// Gets the error message if validation failed
    /// </summary>
    public string ErrorMessage { get; init; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the ValidationResult class
    /// </summary>
    /// <param name="isValid">Whether the validation was successful</param>
    /// <param name="errorMessage">The error message if validation failed</param>
    private ValidationResult(bool isValid, string errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage ?? string.Empty;
    }

    /// <summary>
    /// Creates a successful validation result
    /// </summary>
    /// <returns>A ValidationResult indicating success</returns>
    public static ValidationResult Success()
    {
        return new ValidationResult(true, string.Empty);
    }

    /// <summary>
    /// Creates a failed validation result with an error message
    /// </summary>
    /// <param name="errorMessage">The error message describing why validation failed</param>
    /// <returns>A ValidationResult indicating failure</returns>
    /// <exception cref="ArgumentNullException">Thrown when errorMessage is null</exception>
    public static ValidationResult Failure(string errorMessage)
    {
        if (errorMessage == null)
            throw new ArgumentNullException(nameof(errorMessage));

        return new ValidationResult(false, errorMessage);
    }
}
