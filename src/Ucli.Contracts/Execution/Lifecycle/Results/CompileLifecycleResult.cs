using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Contracts.Execution.Lifecycle;

/// <summary>
/// Represents the provider-independent typed result owned by one compile Lifecycle Execution.
/// </summary>
public sealed record CompileLifecycleResult
{
    /// <summary> Initializes one validated compile result from its action-owned evidence. </summary>
    [JsonConstructor]
    public CompileLifecycleResult (
        RefreshEvidence Refresh,
        ScriptCompilationEvidence ScriptCompilation,
        DomainReloadEvidence DomainReload,
        LifecycleEvidence Lifecycle)
    {
        this.Refresh = ContractArgumentGuard.RequireNotNull(Refresh, nameof(Refresh));
        this.ScriptCompilation = ContractArgumentGuard.RequireNotNull(ScriptCompilation, nameof(ScriptCompilation));
        this.DomainReload = ContractArgumentGuard.RequireNotNull(DomainReload, nameof(DomainReload));
        this.Lifecycle = ContractArgumentGuard.RequireNotNull(Lifecycle, nameof(Lifecycle));
    }

    /// <summary> Gets AssetDatabase refresh evidence for the compile action. </summary>
    [JsonInclude]
    [JsonRequired]
    public RefreshEvidence Refresh { get; private init; }

    /// <summary> Gets script-compilation evidence and normalized compiler diagnostics. </summary>
    [JsonInclude]
    [JsonRequired]
    public ScriptCompilationEvidence ScriptCompilation { get; private init; }

    /// <summary> Gets domain-reload evidence for the compile action. </summary>
    [JsonInclude]
    [JsonRequired]
    public DomainReloadEvidence DomainReload { get; private init; }

    /// <summary> Gets the final lifecycle evidence available to the compile action. </summary>
    [JsonInclude]
    [JsonRequired]
    public LifecycleEvidence Lifecycle { get; private init; }

    /// <summary> Represents AssetDatabase refresh evidence for a compile run. </summary>
    public sealed record RefreshEvidence
    {
        /// <summary> Initializes validated AssetDatabase refresh evidence. </summary>
        [JsonConstructor]
        public RefreshEvidence (
            CompileLifecycleRefreshOrigin Origin,
            bool Requested,
            DateTimeOffset StartedAtUtc,
            DateTimeOffset? CompletedAtUtc,
            bool Completed)
        {
            if (!TextVocabulary.IsDefined(Origin))
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

        public CompileLifecycleRefreshOrigin Origin { get; }

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
            if (CompileGenerationBefore.HasValue
                && CompileGenerationAfter.HasValue
                && CompileGenerationAfter.Value < CompileGenerationBefore.Value)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(CompileGenerationAfter),
                    CompileGenerationAfter,
                    "Compile generation must not regress.");
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
            UnityEditorPrimaryDiagnostic? PrimaryDiagnostic)
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

        public UnityEditorPrimaryDiagnostic? PrimaryDiagnostic { get; }
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
            if (GenerationBefore.HasValue
                && GenerationAfter.HasValue
                && GenerationAfter.Value < GenerationBefore.Value)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(GenerationAfter),
                    GenerationAfter,
                    "Domain reload generation must not regress.");
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
            UnityEditorActionRequired? ActionRequired,
            UnityEditorPrimaryDiagnostic? PrimaryDiagnostic)
        {
            if (ActionRequired.HasValue && !TextVocabulary.IsDefined(ActionRequired.Value))
            {
                throw new ArgumentOutOfRangeException(nameof(ActionRequired), ActionRequired, "Unsupported Unity Editor recovery action.");
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

        public UnityEditorActionRequired? ActionRequired { get; }

        public UnityEditorPrimaryDiagnostic? PrimaryDiagnostic { get; }

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
