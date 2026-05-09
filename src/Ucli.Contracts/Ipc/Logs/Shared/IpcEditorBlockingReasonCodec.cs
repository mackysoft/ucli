using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Converts IPC editor-blocking reason values to canonical literals. </summary>
public static class IpcEditorBlockingReasonCodec
{
    /// <summary> Gets the blocking reason used while editor startup is still in progress. </summary>
    public const string Startup = "startup";

    /// <summary> Gets the blocking reason used while editor internal busy work is running. </summary>
    public const string Busy = "busy";

    /// <summary> Gets the blocking reason used while daemon endpoint recovery is still running. </summary>
    public const string Recovery = "recovery";

    /// <summary> Gets the blocking reason used while script compilation is active. </summary>
    public const string Compile = "compile";

    /// <summary> Gets the blocking reason used after script compilation has failed. </summary>
    public const string CompileFailed = "compileFailed";

    /// <summary> Gets the blocking reason used while domain reload is active. </summary>
    public const string DomainReload = "domainReload";

    /// <summary> Gets the blocking reason used while AssetDatabase refresh or reimport work is active. </summary>
    public const string Reimport = "reimport";

    /// <summary> Gets the blocking reason used while Play Mode is active. </summary>
    public const string PlayMode = "playMode";

    /// <summary> Gets the blocking reason used while a modal dialog blocks the editor. </summary>
    public const string ModalDialog = "modalDialog";

    /// <summary> Gets the blocking reason used while Safe Mode blocks execution. </summary>
    public const string SafeMode = "safeMode";

    /// <summary> Gets the blocking reason used while shutdown is in progress. </summary>
    public const string Shutdown = "shutdown";

    /// <summary> Gets the blocking reason used when editor lifecycle cannot be observed. </summary>
    public const string Unavailable = "unavailable";

    private static readonly string[] CanonicalLiterals =
    {
        Startup,
        Busy,
        Recovery,
        Compile,
        CompileFailed,
        DomainReload,
        Reimport,
        PlayMode,
        ModalDialog,
        SafeMode,
        Shutdown,
        Unavailable,
    };

    /// <summary> Tries to normalize one raw blocking reason literal. </summary>
    /// <param name="value"> The optional raw literal. </param>
    /// <param name="blockingReason"> The canonical literal on success; otherwise <see langword="null" />. </param>
    /// <returns> <see langword="true" /> when one supported literal is normalized; otherwise <see langword="false" />. </returns>
    public static bool TryParse (
        string? value,
        out string? blockingReason)
    {
        return LiteralCodecUtilities.TryNormalizeLiteral(
            value,
            CanonicalLiterals,
            StringComparison.Ordinal,
            out blockingReason);
    }
}
