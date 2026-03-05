using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts daemon-log query-target values to canonical IPC literals. </summary>
public static class IpcDaemonLogsQueryTargetCodec
{
    /// <summary> Gets the query-target value for searching message only. </summary>
    public const string Message = "message";

    /// <summary> Gets the query-target value for searching raw stack only. </summary>
    public const string Stack = "stack";

    /// <summary> Gets the query-target value for searching both message and raw. </summary>
    public const string Both = "both";

    private static readonly string[] CanonicalLiterals =
    {
        Message,
        Stack,
        Both,
    };

    /// <summary> Tries to parse one query-target literal to canonical IPC value. </summary>
    /// <param name="value"> The optional query-target literal. </param>
    /// <param name="queryTarget"> The normalized query-target literal when operation succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when one supported query-target value is normalized; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out string? queryTarget)
    {
        return LiteralCodecUtilities.TryNormalizeLiteral(
            value,
            CanonicalLiterals,
            StringComparison.OrdinalIgnoreCase,
            out queryTarget);
    }
}