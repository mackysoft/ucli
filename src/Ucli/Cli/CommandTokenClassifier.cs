namespace MackySoft.Ucli.Cli;

/// <summary> Provides reusable classifiers for command-line token semantics. </summary>
internal static class CommandTokenClassifier
{
    /// <summary> Determines whether a token should be treated as root-command context. </summary>
    /// <param name="token"> The command-line token. </param>
    /// <returns> <see langword="true" /> when token is empty, whitespace, or an option token; otherwise <see langword="false" />. </returns>
    public static bool IsRootCommandToken (string? token)
    {
        return string.IsNullOrWhiteSpace(token)
            || token.StartsWith("-", StringComparison.Ordinal);
    }
}