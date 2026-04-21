using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts Unity-log source filter values to canonical IPC literals. </summary>
public static class IpcUnityLogsSourceCodec
{
    /// <summary> Gets the compile-log source literal. </summary>
    public const string Compile = "compile";

    /// <summary> Gets the runtime-log source literal. </summary>
    public const string Runtime = "runtime";

    /// <summary> Gets the source literal that disables source filtering. </summary>
    public const string All = "all";

    private static readonly string[] CanonicalLiterals =
    {
        Compile,
        Runtime,
        All,
    };

    /// <summary> Tries to parse one source literal to canonical IPC value. </summary>
    /// <param name="value"> The optional source literal. </param>
    /// <param name="source"> The normalized source literal when operation succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when one supported source value is normalized; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out string? source)
    {
        return LiteralCodecUtilities.TryNormalizeLiteral(
            value,
            CanonicalLiterals,
            StringComparison.OrdinalIgnoreCase,
            out source);
    }

    /// <summary> Determines whether one optional source value represents <see cref="All" />. </summary>
    /// <param name="value"> The optional source value. </param>
    /// <returns> <see langword="true" /> when <paramref name="value" /> maps to <see cref="All" />; otherwise <see langword="false" />. </returns>
    public static bool IsAll (string? value)
    {
        return TryParse(value, out var source)
            && string.Equals(source, All, StringComparison.Ordinal);
    }
}