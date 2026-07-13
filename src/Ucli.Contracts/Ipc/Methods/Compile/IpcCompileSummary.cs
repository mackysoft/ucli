using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents persisted compile assurance evidence captured inside Unity. </summary>
public sealed record IpcCompileSummary
{
    /// <summary> Initializes one validated compile assurance summary. </summary>
    [JsonConstructor]
    public IpcCompileSummary (
        string RunId,
        ProjectFingerprint ProjectFingerprint,
        bool Completed,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        RefreshEvidence Refresh,
        ScriptCompilationEvidence ScriptCompilation,
        DomainReloadEvidence DomainReload,
        LifecycleEvidence Lifecycle)
    {
        this.RunId = ContractArgumentGuard.RequireValue(RunId, nameof(RunId));
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.Completed = Completed;
        this.StartedAtUtc = StartedAtUtc;
        this.CompletedAtUtc = CompletedAtUtc;
        this.Refresh = ContractArgumentGuard.RequireNotNull(Refresh, nameof(Refresh));
        this.ScriptCompilation = ContractArgumentGuard.RequireNotNull(ScriptCompilation, nameof(ScriptCompilation));
        this.DomainReload = ContractArgumentGuard.RequireNotNull(DomainReload, nameof(DomainReload));
        this.Lifecycle = ContractArgumentGuard.RequireNotNull(Lifecycle, nameof(Lifecycle));
    }

    public string RunId { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public bool Completed { get; init; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public RefreshEvidence Refresh { get; init; }

    public ScriptCompilationEvidence ScriptCompilation { get; init; }

    public DomainReloadEvidence DomainReload { get; init; }

    public LifecycleEvidence Lifecycle { get; init; }

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
        string CompileGenerationBefore,
        string CompileGenerationAfter,
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
        string GenerationBefore,
        string GenerationAfter,
        bool Settled);

    /// <summary> Represents the final lifecycle snapshot after compile observation. </summary>
    public sealed record LifecycleEvidence (
        string? ServerVersion,
        string? UnityVersion,
        string? EditorMode,
        string? LifecycleState,
        string? BlockingReason,
        string? CompileState,
        string? CompileGeneration,
        string? DomainReloadGeneration,
        bool CanAcceptExecutionRequests,
        DateTimeOffset? ObservedAtUtc,
        string? ActionRequired,
        IpcPrimaryDiagnostic? PrimaryDiagnostic);
}
