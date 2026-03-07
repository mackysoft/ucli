using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts Unity-log stack-trace mode values to canonical IPC literals. </summary>
public static class IpcUnityLogsStackTraceModeCodec
{
    /// <summary> Gets the stack-trace mode that suppresses stack traces. </summary>
    public const string None = "none";

    /// <summary> Gets the stack-trace mode that includes stack traces for error events only. </summary>
    public const string Error = "error";

    /// <summary> Gets the stack-trace mode that includes stack traces for all events. </summary>
    public const string All = "all";

    private static readonly string[] CanonicalLiterals =
    {
        None,
        Error,
        All,
    };

    /// <summary> Tries to parse one stack-trace mode literal to canonical IPC value. </summary>
    /// <param name="value"> The optional stack-trace mode literal. </param>
    /// <param name="stackTraceMode"> The normalized stack-trace mode literal when operation succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when one supported mode is normalized; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out string? stackTraceMode)
    {
        return LiteralCodecUtilities.TryNormalizeLiteral(
            value,
            CanonicalLiterals,
            StringComparison.OrdinalIgnoreCase,
            out stackTraceMode);
    }
}