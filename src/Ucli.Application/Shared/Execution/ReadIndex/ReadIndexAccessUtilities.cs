namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex;

/// <summary> Provides shared read-index access helpers for application policy. </summary>
internal static class ReadIndexAccessUtilities
{
    /// <summary> Combines two fallback reason fragments into one user-facing sentence. </summary>
    public static string? CombineFallbackReasons (
        string? first,
        string? second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return string.IsNullOrWhiteSpace(second) ? null : second;
        }

        if (string.IsNullOrWhiteSpace(second))
        {
            return first;
        }

        return $"{first} {second}";
    }
}
