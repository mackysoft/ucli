namespace MackySoft.Ucli.Contracts.Text;

/// <summary> Provides reusable validation helpers for string values. </summary>
internal static class StringValueValidator
{
    /// <summary> Determines whether a value contains leading or trailing whitespace. </summary>
    /// <param name="value"> The string value to inspect. </param>
    /// <returns> <see langword="true" /> when leading or trailing whitespace exists; otherwise <see langword="false" />. </returns>
    public static bool HasOuterWhitespace (string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        return char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[value.Length - 1]);
    }
}