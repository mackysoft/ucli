using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Assurance;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents persisted compile assurance evidence captured inside Unity. </summary>
public sealed record IpcCompileSummary
{
    /// <summary> Initializes one validated compile assurance summary. </summary>
    [JsonConstructor]
    public IpcCompileSummary (
        Guid RunId,
        ProjectFingerprint ProjectFingerprint,
        bool Completed,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        RefreshEvidence Refresh,
        ScriptCompilationEvidence ScriptCompilation,
        DomainReloadEvidence DomainReload,
        LifecycleEvidence Lifecycle)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.Completed = Completed;
        this.StartedAtUtc = StartedAtUtc;
        this.CompletedAtUtc = CompletedAtUtc;
        this.Refresh = ContractArgumentGuard.RequireNotNull(Refresh, nameof(Refresh));
        this.ScriptCompilation = ContractArgumentGuard.RequireNotNull(ScriptCompilation, nameof(ScriptCompilation));
        this.DomainReload = ContractArgumentGuard.RequireNotNull(DomainReload, nameof(DomainReload));
        this.Lifecycle = ContractArgumentGuard.RequireNotNull(Lifecycle, nameof(Lifecycle));
    }

    public Guid RunId { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public bool Completed { get; init; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset? CompletedAtUtc { get; init; }

    public RefreshEvidence Refresh { get; init; }

    public ScriptCompilationEvidence ScriptCompilation { get; init; }

    public DomainReloadEvidence DomainReload { get; init; }

    public LifecycleEvidence Lifecycle { get; init; }

    /// <summary> Represents AssetDatabase refresh evidence for a compile run. </summary>
    public sealed record RefreshEvidence
    {
        /// <summary> Initializes validated AssetDatabase refresh evidence. </summary>
        [JsonConstructor]
        public RefreshEvidence (
            CompileRefreshOrigin Origin,
            bool Requested,
            DateTimeOffset StartedAtUtc,
            DateTimeOffset? CompletedAtUtc,
            bool Completed)
        {
            if (!ContractLiteralCodec.IsDefined(Origin))
            {
                throw new ArgumentOutOfRangeException(nameof(Origin), Origin, "Compile refresh origin must be defined.");
            }

            this.Origin = Origin;
            this.Requested = Requested;
            this.StartedAtUtc = StartedAtUtc;
            this.CompletedAtUtc = CompletedAtUtc;
            this.Completed = Completed;
        }

        public CompileRefreshOrigin Origin { get; }

        public bool Requested { get; }

        public DateTimeOffset StartedAtUtc { get; }

        public DateTimeOffset? CompletedAtUtc { get; }

        public bool Completed { get; }
    }

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
    public sealed record LifecycleEvidence
    {
        /// <summary> Initializes final lifecycle evidence with an optional recognized recovery action. </summary>
        [JsonConstructor]
        public LifecycleEvidence (
            string? ServerVersion,
            string? UnityVersion,
            UnityEditorStateSnapshot? State,
            DateTimeOffset? ObservedAtUtc,
            DaemonDiagnosisActionRequired? ActionRequired,
            IpcPrimaryDiagnostic? PrimaryDiagnostic)
        {
            if (ActionRequired.HasValue && !ContractLiteralCodec.IsDefined(ActionRequired.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(ActionRequired), ActionRequired, "Unsupported daemon diagnosis action.");
            }

            this.ServerVersion = ServerVersion;
            this.UnityVersion = UnityVersion;
            this.State = State;
            this.ObservedAtUtc = ObservedAtUtc;
            this.ActionRequired = ActionRequired;
            this.PrimaryDiagnostic = PrimaryDiagnostic;
        }

        public string? ServerVersion { get; }

        public string? UnityVersion { get; }

        [JsonInclude]
        [JsonRequired]
        public UnityEditorStateSnapshot? State { get; private init; }

        public DateTimeOffset? ObservedAtUtc { get; }

        public DaemonDiagnosisActionRequired? ActionRequired { get; }

        public IpcPrimaryDiagnostic? PrimaryDiagnostic { get; }
    }
}
