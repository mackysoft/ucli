namespace MackySoft.Ucli.Contracts;

/// <summary> Enforces constructor invariants shared by contract models. </summary>
internal static class ContractArgumentGuard
{
    /// <summary> Requires a non-null reference value. </summary>
    public static T RequireNotNull<T> (
        T? value,
        string parameterName)
        where T : class
    {
        return value ?? throw new ArgumentNullException(parameterName);
    }

    /// <summary> Requires text containing at least one non-whitespace character. </summary>
    public static string RequireValue (
        string? value,
        string parameterName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value must not be empty or whitespace.", parameterName);
        }

        return value;
    }

    /// <summary> Requires a value greater than zero. </summary>
    public static int RequirePositive (
        int value,
        string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must be positive.");
        }

        return value;
    }

    /// <summary> Requires a value greater than or equal to zero. </summary>
    public static int RequireNonNegative (
        int value,
        string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, value, "Value must not be negative.");
        }

        return value;
    }
}
