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

        var validatedStartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(StartedAtUtc, nameof(StartedAtUtc));
        this.RunId = RunId;
        this.ProjectFingerprint = ContractArgumentGuard.RequireNotNull(ProjectFingerprint, nameof(ProjectFingerprint));
        this.Completed = Completed;
        this.StartedAtUtc = validatedStartedAtUtc;
        this.CompletedAtUtc = RequireCompletionTimestamp(
            Completed,
            CompletedAtUtc,
            validatedStartedAtUtc,
            nameof(CompletedAtUtc));
        this.Refresh = ContractArgumentGuard.RequireNotNull(Refresh, nameof(Refresh));
        this.ScriptCompilation = ContractArgumentGuard.RequireNotNull(ScriptCompilation, nameof(ScriptCompilation));
        this.DomainReload = ContractArgumentGuard.RequireNotNull(DomainReload, nameof(DomainReload));
        this.Lifecycle = ContractArgumentGuard.RequireNotNull(Lifecycle, nameof(Lifecycle));
    }

    public Guid RunId { get; }

    public ProjectFingerprint ProjectFingerprint { get; }

    public bool Completed { get; }

    public DateTimeOffset StartedAtUtc { get; }

    public DateTimeOffset? CompletedAtUtc { get; }

    public RefreshEvidence Refresh { get; }

    public ScriptCompilationEvidence ScriptCompilation { get; }

    public DomainReloadEvidence DomainReload { get; }

    public LifecycleEvidence Lifecycle { get; }

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

            var validatedStartedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(StartedAtUtc, nameof(StartedAtUtc));
            this.Origin = Origin;
            this.Requested = Requested;
            this.StartedAtUtc = validatedStartedAtUtc;
            this.CompletedAtUtc = RequireCompletionTimestamp(
                Completed,
                CompletedAtUtc,
                validatedStartedAtUtc,
                nameof(CompletedAtUtc));
            this.Completed = Completed;
        }

        public CompileRefreshOrigin Origin { get; }

        public bool Requested { get; }

        public DateTimeOffset StartedAtUtc { get; }

        public DateTimeOffset? CompletedAtUtc { get; }

        public bool Completed { get; }
    }

    /// <summary> Represents script compilation evidence for a compile run. </summary>
    public sealed record ScriptCompilationEvidence
    {
        [JsonConstructor]
        public ScriptCompilationEvidence (
            bool Started,
            bool Completed,
            long? CompileGenerationBefore,
            long? CompileGenerationAfter,
            DiagnosticsEvidence Diagnostics)
        {
            if (CompileGenerationBefore is < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(CompileGenerationBefore),
                    CompileGenerationBefore,
                    "Compile generation must not be negative.");
            }

            if (CompileGenerationAfter is < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(CompileGenerationAfter),
                    CompileGenerationAfter,
                    "Compile generation must not be negative.");
            }

            this.Started = Started;
            this.Completed = Completed;
            this.CompileGenerationBefore = CompileGenerationBefore;
            this.CompileGenerationAfter = CompileGenerationAfter;
            this.Diagnostics = ContractArgumentGuard.RequireNotNull(Diagnostics, nameof(Diagnostics));
        }

        public bool Started { get; }

        public bool Completed { get; }

        public long? CompileGenerationBefore { get; }

        public long? CompileGenerationAfter { get; }

        public DiagnosticsEvidence Diagnostics { get; }
    }

    /// <summary> Represents compiler diagnostic counts and the primary diagnostic. </summary>
    public sealed record DiagnosticsEvidence
    {
        [JsonConstructor]
        public DiagnosticsEvidence (
            int ErrorCount,
            int WarningCount,
            IpcPrimaryDiagnostic? PrimaryDiagnostic)
        {
            if (ErrorCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(ErrorCount), ErrorCount, "Error count must not be negative.");
            }

            if (WarningCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(WarningCount), WarningCount, "Warning count must not be negative.");
            }

            this.ErrorCount = ErrorCount;
            this.WarningCount = WarningCount;
            this.PrimaryDiagnostic = PrimaryDiagnostic;
        }

        public int ErrorCount { get; }

        public int WarningCount { get; }

        public IpcPrimaryDiagnostic? PrimaryDiagnostic { get; }
    }

    /// <summary> Represents domain reload evidence for a compile run. </summary>
    public sealed record DomainReloadEvidence
    {
        [JsonConstructor]
        public DomainReloadEvidence (
            bool ReloadRequired,
            bool ReloadObserved,
            long? GenerationBefore,
            long? GenerationAfter,
            bool Settled)
        {
            if (GenerationBefore is < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(GenerationBefore),
                    GenerationBefore,
                    "Domain reload generation must not be negative.");
            }

            if (GenerationAfter is < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(GenerationAfter),
                    GenerationAfter,
                    "Domain reload generation must not be negative.");
            }

            this.ReloadRequired = ReloadRequired;
            this.ReloadObserved = ReloadObserved;
            this.GenerationBefore = GenerationBefore;
            this.GenerationAfter = GenerationAfter;
            this.Settled = Settled;
        }

        public bool ReloadRequired { get; }

        public bool ReloadObserved { get; }

        public long? GenerationBefore { get; }

        public long? GenerationAfter { get; }

        public bool Settled { get; }
    }

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

            this.ServerVersion = RequireOptionalVersion(ServerVersion, nameof(ServerVersion));
            this.UnityVersion = RequireOptionalVersion(UnityVersion, nameof(UnityVersion));
            this.State = State;
            this.ObservedAtUtc = ObservedAtUtc.HasValue
                ? ContractArgumentGuard.RequireUtcTimestamp(ObservedAtUtc.Value, nameof(ObservedAtUtc))
                : null;
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

        private static string? RequireOptionalVersion (
            string? value,
            string parameterName)
        {
            if (value is null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(value) || StringValueValidator.HasOuterWhitespace(value))
            {
                throw new ArgumentException(
                    "Version must not be empty or contain outer whitespace.",
                    parameterName);
            }

            return value;
        }
    }

    private static DateTimeOffset? RequireCompletionTimestamp (
        bool completed,
        DateTimeOffset? completedAtUtc,
        DateTimeOffset startedAtUtc,
        string parameterName)
    {
        if (completed != completedAtUtc.HasValue)
        {
            throw new ArgumentException(
                "Completion timestamp presence must match the completed state.",
                parameterName);
        }

        if (!completedAtUtc.HasValue)
        {
            return null;
        }

        var validatedCompletedAtUtc = ContractArgumentGuard.RequireUtcTimestamp(
            completedAtUtc.Value,
            parameterName);
        if (validatedCompletedAtUtc < startedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                validatedCompletedAtUtc,
                "Completion timestamp must not precede the start timestamp.");
        }

        return validatedCompletedAtUtc;
    }
}
