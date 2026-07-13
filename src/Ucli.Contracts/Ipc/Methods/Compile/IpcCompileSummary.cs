using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents persisted compile assurance evidence captured inside Unity. </summary>
/// <param name="RunId"> The compile run identifier. </param>
/// <param name="ProjectFingerprint"> The project fingerprint served by the Unity IPC host. </param>
/// <param name="Completed"> Whether the compile run reached a terminal observation. </param>
/// <param name="StartedAtUtc"> The UTC timestamp when the compile run started. </param>
/// <param name="CompletedAtUtc"> The UTC timestamp when the compile run completed. </param>
/// <param name="Refresh"> AssetDatabase refresh evidence. </param>
/// <param name="ScriptCompilation"> Script compilation evidence. </param>
/// <param name="DomainReload"> Domain reload evidence. </param>
/// <param name="Lifecycle"> Final lifecycle evidence after compile observation. </param>
public sealed record IpcCompileSummary (
    string RunId,
    string ProjectFingerprint,
    bool Completed,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IpcCompileSummary.RefreshEvidence Refresh,
    IpcCompileSummary.ScriptCompilationEvidence ScriptCompilation,
    IpcCompileSummary.DomainReloadEvidence DomainReload,
    IpcCompileSummary.LifecycleEvidence Lifecycle)
{
    /// <summary> Represents AssetDatabase refresh evidence for a compile run. </summary>
    public sealed record RefreshEvidence (
        string Origin,
        bool Requested,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        bool Completed);

    /// <summary> Represents script compilation evidence for a compile run. </summary>
    public sealed record ScriptCompilationEvidence (
        bool Started,
        bool Completed,
        long? CompileGenerationBefore,
        long? CompileGenerationAfter,
        DiagnosticsEvidence Diagnostics);

    /// <summary> Represents compiler diagnostic counts and the primary diagnostic. </summary>
    public sealed record DiagnosticsEvidence (
        int ErrorCount,
        int WarningCount,
        IpcPrimaryDiagnostic? PrimaryDiagnostic);

    /// <summary> Represents domain reload evidence for a compile run. </summary>
    public sealed record DomainReloadEvidence (
        bool ReloadRequired,
        bool ReloadObserved,
        long? GenerationBefore,
        long? GenerationAfter,
        bool Settled);

    /// <summary> Represents the final lifecycle snapshot after compile observation. </summary>
    public sealed record LifecycleEvidence (
        string? ServerVersion,
        string? UnityVersion,
        [property: JsonRequired]
        UnityEditorStateSnapshot? State,
        DateTimeOffset? ObservedAtUtc,
        string? ActionRequired,
        IpcPrimaryDiagnostic? PrimaryDiagnostic);
}
