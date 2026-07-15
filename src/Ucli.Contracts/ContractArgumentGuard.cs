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

    /// <summary> Creates a stable snapshot from a non-null collection containing no null items. </summary>
    public static IReadOnlyList<T> RequireItems<T> (
        IReadOnlyList<T>? value,
        string parameterName)
        where T : class
    {
        if (value == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (value.Count == 0)
        {
            return Array.Empty<T>();
        }

        var items = new T[value.Count];
        for (var index = 0; index < value.Count; index++)
        {
            var item = value[index];
            if (item == null)
            {
                throw new ArgumentException($"Collection item at index {index} must not be null.", parameterName);
            }

            items[index] = item;
        }

        return Array.AsReadOnly(items);
    }

    /// <summary> Creates a stable snapshot from non-empty text values. </summary>
    public static IReadOnlyList<string> RequireValues (
        IReadOnlyList<string>? value,
        string parameterName)
    {
        if (value == null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (value.Count == 0)
        {
            return Array.Empty<string>();
        }

        var items = new string[value.Count];
        for (var index = 0; index < value.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(value[index]))
            {
                throw new ArgumentException(
                    $"Collection item at index {index} must not be empty or whitespace.",
                    parameterName);
            }

            items[index] = value[index];
        }

        return Array.AsReadOnly(items);
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

    /// <summary> Requires a non-empty globally unique identifier. </summary>
    public static Guid RequireNonEmptyGuid (
        Guid value,
        string parameterName)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("GUID must not be empty.", parameterName);
        }

        return value;
    }

    /// <summary> Requires a non-default timestamp. </summary>
    public static DateTimeOffset RequireTimestamp (
        DateTimeOffset value,
        string parameterName)
    {
        if (value == default)
        {
            throw new ArgumentException("Timestamp must not be the default value.", parameterName);
        }

        return value;
    }

    /// <summary> Requires a non-default timestamp expressed with the UTC offset. </summary>
    public static DateTimeOffset RequireUtcTimestamp (
        DateTimeOffset value,
        string parameterName)
    {
        RequireTimestamp(value, parameterName);

        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("UTC timestamp must use the zero offset.", parameterName);
        }

        return value;
    }
}
