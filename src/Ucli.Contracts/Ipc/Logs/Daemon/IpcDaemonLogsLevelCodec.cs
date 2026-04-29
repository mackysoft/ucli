using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts daemon-log level filter values to canonical IPC literals. </summary>
public static class IpcDaemonLogsLevelCodec
{
    /// <summary> Gets the level filter value that allows all levels. </summary>
    public const string All = "all";

    /// <summary> Gets the level filter value for error logs. </summary>
    public const string Error = "error";

    /// <summary> Gets the level filter value for warning logs. </summary>
    public const string Warning = "warning";

    /// <summary> Gets the level filter value for info logs. </summary>
    public const string Info = "info";

    private static readonly string[] CanonicalLiterals =
    {
        All,
        Error,
        Warning,
        Info,
    };

    /// <summary> Tries to parse one level literal to canonical IPC value. </summary>
    /// <param name="value"> The optional level literal. </param>
    /// <param name="level"> The normalized level literal when operation succeeds; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when one supported level value is normalized; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out string? level)
    {
        return LiteralCodecUtilities.TryNormalizeLiteral(
            value,
            CanonicalLiterals,
            StringComparison.OrdinalIgnoreCase,
            out level);
    }
}
