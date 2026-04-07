using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts IPC editor lifecycle-state values to canonical literals. </summary>
public static class IpcEditorLifecycleStateCodec
{
    /// <summary> Gets the lifecycle state used while startup work is still running. </summary>
    public const string Starting = "starting";

    /// <summary> Gets the lifecycle state used while execution requests are accepted. </summary>
    public const string Ready = "ready";

    /// <summary> Gets the lifecycle state used while editor-internal busy work blocks execution. </summary>
    public const string Busy = "busy";

    /// <summary> Gets the lifecycle state used while script compilation is active. </summary>
    public const string Compiling = "compiling";

    /// <summary> Gets the lifecycle state used while domain reload is active. </summary>
    public const string DomainReloading = "domainReloading";

    /// <summary> Gets the lifecycle state used while Play Mode is active. </summary>
    public const string Playmode = "playmode";

    /// <summary> Gets the lifecycle state used while a modal dialog blocks the editor. </summary>
    public const string BlockedByModal = "blockedByModal";

    /// <summary> Gets the lifecycle state used while Safe Mode blocks normal execution. </summary>
    public const string SafeMode = "safeMode";

    /// <summary> Gets the lifecycle state used while shutdown is in progress. </summary>
    public const string ShuttingDown = "shuttingDown";

    private static readonly string[] CanonicalLiterals =
    {
        Starting,
        Ready,
        Busy,
        Compiling,
        DomainReloading,
        Playmode,
        BlockedByModal,
        SafeMode,
        ShuttingDown,
    };

    /// <summary> Tries to normalize one raw lifecycle-state literal. </summary>
    /// <param name="value"> The optional raw literal. </param>
    /// <param name="lifecycleState"> The canonical literal on success; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when one supported literal is normalized; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out string? lifecycleState)
    {
        return LiteralCodecUtilities.TryNormalizeLiteral(
            value,
            CanonicalLiterals,
            StringComparison.Ordinal,
            out lifecycleState);
    }
}