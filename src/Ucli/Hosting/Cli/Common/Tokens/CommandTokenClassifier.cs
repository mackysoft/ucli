namespace MackySoft.Ucli.Hosting.Cli.Common.Tokens;

/// <summary> Provides reusable classifiers for command-line token semantics. </summary>
internal static class CommandTokenClassifier
{
    private const string HelpOptionLong = "--help";

    private const string HelpOptionShort = "-h";

    private const string VersionOptionLong = "--version";

    private const string VersionOptionShort = "-v";

    /// <summary> Determines whether a token should be treated as root-command context. </summary>
    /// <param name="token"> The command-line token. </param>
    /// <returns> <see langword="true" /> when token is empty, whitespace, or an option token; otherwise <see langword="false" />. </returns>
    public static bool IsRootCommandToken (string? token)
    {
        return string.IsNullOrWhiteSpace(token)
            || token.StartsWith("-", StringComparison.Ordinal);
    }

    /// <summary> Determines whether a token is an option token. </summary>
    /// <param name="token"> The command-line token. </param>
    /// <returns> <see langword="true" /> when token starts with <c>-</c>; otherwise <see langword="false" />. </returns>
    public static bool IsOptionToken (string? token)
    {
        return !string.IsNullOrWhiteSpace(token)
            && token.StartsWith("-", StringComparison.Ordinal);
    }

    /// <summary> Determines whether a token is a help option token. </summary>
    /// <param name="token"> The command-line token. </param>
    /// <returns> <see langword="true" /> when token is <c>-h</c> or <c>--help</c>; otherwise <see langword="false" />. </returns>
    public static bool IsHelpOptionToken (string? token)
    {
        return string.Equals(token, HelpOptionShort, StringComparison.Ordinal)
            || string.Equals(token, HelpOptionLong, StringComparison.Ordinal);
    }

    /// <summary> Determines whether a token is a version option token. </summary>
    /// <param name="token"> The command-line token. </param>
    /// <returns> <see langword="true" /> when token is <c>-v</c> or <c>--version</c>; otherwise <see langword="false" />. </returns>
    public static bool IsVersionOptionToken (string? token)
    {
        return string.Equals(token, VersionOptionShort, StringComparison.Ordinal)
            || string.Equals(token, VersionOptionLong, StringComparison.Ordinal);
    }
}
