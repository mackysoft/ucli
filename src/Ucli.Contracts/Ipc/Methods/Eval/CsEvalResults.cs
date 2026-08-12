using System.Text.Json;
using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Projects;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents compiler output from a dedicated C# evaluation stage. </summary>
[Description("C# evaluation compiler result.")]
public sealed record CsEvalPlanCompileResult
{
    [JsonConstructor]
    public CsEvalPlanCompileResult (bool succeeded, IReadOnlyList<CsEvalDiagnostic> diagnostics)
    {
        Diagnostics = ContractArgumentGuard.RequireItems(diagnostics, nameof(diagnostics));
        if (succeeded && Diagnostics.Any(static diagnostic => diagnostic.Severity == UcliDiagnosticSeverity.Error))
        {
            throw new ArgumentException("Successful compilation cannot include error diagnostics.", nameof(diagnostics));
        }

        if (!succeeded && !Diagnostics.Any(static diagnostic => diagnostic.Severity == UcliDiagnosticSeverity.Error))
        {
            throw new ArgumentException("Failed compilation must include an error diagnostic.", nameof(diagnostics));
        }

        Succeeded = succeeded;
    }

    [JsonInclude, JsonRequired]
    public bool Succeeded { get; private init; }

    [JsonInclude, JsonRequired]
    public IReadOnlyList<CsEvalDiagnostic> Diagnostics { get; private init; }
}

/// <summary> Represents the successful static plan for C# evaluation. </summary>
[Description("Successful eval.plan result.")]
public sealed record CsEvalPlanSuccessResult
{
    [JsonConstructor]
    public CsEvalPlanSuccessResult (Sha256Digest sourceDigest, CsEvalSourceKind sourceKind, string resolvedEntryPoint, Sha256Digest executionDigest, CsEvalPlanCompileResult compile)
    {
        SourceDigest = sourceDigest ?? throw new ArgumentNullException(nameof(sourceDigest));
        if (!TextVocabulary.IsDefined(sourceKind)) throw new ArgumentOutOfRangeException(nameof(sourceKind));
        SourceKind = sourceKind;
        ResolvedEntryPoint = ContractArgumentGuard.RequireValue(resolvedEntryPoint, nameof(resolvedEntryPoint));
        ExecutionDigest = executionDigest ?? throw new ArgumentNullException(nameof(executionDigest));
        Compile = compile ?? throw new ArgumentNullException(nameof(compile));
        if (!Compile.Succeeded) throw new ArgumentException("Plan success requires a successful compilation.", nameof(compile));
    }

    [JsonInclude, JsonRequired] public Sha256Digest SourceDigest { get; private init; }
    [JsonInclude, JsonRequired] public CsEvalSourceKind SourceKind { get; private init; }
    [JsonInclude, JsonRequired] public string ResolvedEntryPoint { get; private init; }
    [JsonInclude, JsonRequired] public Sha256Digest ExecutionDigest { get; private init; }
    [JsonInclude, JsonRequired] public CsEvalPlanCompileResult Compile { get; private init; }
}

/// <summary> Represents the successful execution result from eval.call. </summary>
[Description("Successful eval.call result.")]
public sealed record CsEvalCallSuccessResult
{
    [JsonConstructor]
    public CsEvalCallSuccessResult (Sha256Digest sourceDigest, CsEvalSourceKind sourceKind, string resolvedEntryPoint, Sha256Digest executionDigest, CsEvalPlanCompileResult compile, long durationMilliseconds, IReadOnlyList<CsEvalLogEntry> logs, CsEvalReturnValue returnValue, CsEvalTouchedResources touchedResources)
    {
        SourceDigest = sourceDigest ?? throw new ArgumentNullException(nameof(sourceDigest));
        if (!TextVocabulary.IsDefined(sourceKind)) throw new ArgumentOutOfRangeException(nameof(sourceKind));
        SourceKind = sourceKind;
        ResolvedEntryPoint = ContractArgumentGuard.RequireValue(resolvedEntryPoint, nameof(resolvedEntryPoint));
        ExecutionDigest = executionDigest ?? throw new ArgumentNullException(nameof(executionDigest));
        Compile = compile ?? throw new ArgumentNullException(nameof(compile));
        if (!Compile.Succeeded) throw new ArgumentException("Call success requires a successful compilation.", nameof(compile));
        if (durationMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(durationMilliseconds));
        DurationMilliseconds = durationMilliseconds;
        Logs = ValidateLogSequence(logs, nameof(logs));
        ReturnValue = returnValue ?? throw new ArgumentNullException(nameof(returnValue));
        TouchedResources = touchedResources ?? throw new ArgumentNullException(nameof(touchedResources));
    }

    [JsonInclude, JsonRequired] public Sha256Digest SourceDigest { get; private init; }
    [JsonInclude, JsonRequired] public CsEvalSourceKind SourceKind { get; private init; }
    [JsonInclude, JsonRequired] public string ResolvedEntryPoint { get; private init; }
    [JsonInclude, JsonRequired] public Sha256Digest ExecutionDigest { get; private init; }
    [JsonInclude, JsonRequired] public CsEvalPlanCompileResult Compile { get; private init; }
    [JsonInclude, JsonRequired] public long DurationMilliseconds { get; private init; }
    [JsonInclude, JsonRequired] public IReadOnlyList<CsEvalLogEntry> Logs { get; private init; }
    [JsonInclude, JsonRequired] public CsEvalReturnValue ReturnValue { get; private init; }
    [JsonInclude, JsonRequired] public CsEvalTouchedResources TouchedResources { get; private init; }

    internal static IReadOnlyList<CsEvalLogEntry> ValidateLogSequence (
        IReadOnlyList<CsEvalLogEntry> logs,
        string parameterName)
    {
        var snapshot = ContractArgumentGuard.RequireItems(logs, parameterName);
        for (var index = 0; index < snapshot.Count; index++)
        {
            var entry = snapshot[index]
                ?? throw new ArgumentException($"Log entry at index {index} must not be null.", parameterName);
            if (entry.Sequence != index + 1)
            {
                throw new ArgumentException("Eval log sequences must start at 1 and increase without gaps.", parameterName);
            }
        }

        return snapshot;
    }
}

/// <summary> Represents recoverable evaluation evidence from a failed stage. </summary>
[Description("Partial eval failure result.")]
public sealed record CsEvalPartialErrorResult
{
    [JsonConstructor]
    public CsEvalPartialErrorResult (Sha256Digest sourceDigest, CsEvalSourceKind sourceKind, string? resolvedEntryPoint, Sha256Digest executionDigest, CsEvalPlanCompileResult compile, long? durationMilliseconds, IReadOnlyList<CsEvalLogEntry>? logs, CsEvalReturnValue? returnValue, CsEvalTouchedResources? touchedResources)
    {
        if (durationMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(durationMilliseconds));
        SourceDigest = sourceDigest ?? throw new ArgumentNullException(nameof(sourceDigest));
        if (!TextVocabulary.IsDefined(sourceKind)) throw new ArgumentOutOfRangeException(nameof(sourceKind));
        SourceKind = sourceKind;
        ResolvedEntryPoint = resolvedEntryPoint;
        ExecutionDigest = executionDigest ?? throw new ArgumentNullException(nameof(executionDigest));
        Compile = compile ?? throw new ArgumentNullException(nameof(compile));
        DurationMilliseconds = durationMilliseconds;
        Logs = logs is null ? null : CsEvalCallSuccessResult.ValidateLogSequence(logs, nameof(logs));
        ReturnValue = returnValue;
        TouchedResources = touchedResources;
    }

    [JsonInclude, JsonRequired] public Sha256Digest SourceDigest { get; private init; }
    [JsonInclude, JsonRequired] public CsEvalSourceKind SourceKind { get; private init; }
    [JsonInclude, JsonRequired] public string? ResolvedEntryPoint { get; private init; }
    [JsonInclude, JsonRequired] public Sha256Digest ExecutionDigest { get; private init; }
    [JsonInclude, JsonRequired] public CsEvalPlanCompileResult Compile { get; private init; }
    [JsonInclude, JsonRequired] public long? DurationMilliseconds { get; private init; }
    [JsonInclude, JsonRequired] public IReadOnlyList<CsEvalLogEntry>? Logs { get; private init; }
    [JsonInclude, JsonRequired] public CsEvalReturnValue? ReturnValue { get; private init; }
    [JsonInclude, JsonRequired] public CsEvalTouchedResources? TouchedResources { get; private init; }

}

/// <summary> Defines a successful dedicated eval IPC response without execute results or idempotency records. </summary>
public sealed record IpcEvalResponse
{
    [JsonConstructor]
    public IpcEvalResponse (
        UnityProjectIdentity project,
        CsEvalPhase phase,
        ExecutionApplicationState applicationState,
        object eval,
        string? planToken,
        ExecutionReadPostcondition? readPostcondition)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        if (!TextVocabulary.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        if (!TextVocabulary.IsDefined(applicationState) || applicationState == ExecutionApplicationState.PartiallyApplied) throw new ArgumentOutOfRangeException(nameof(applicationState));
        if (eval is not CsEvalPlanSuccessResult && eval is not CsEvalCallSuccessResult)
        {
            throw new ArgumentException("Eval success response must contain a closed success result.", nameof(eval));
        }

        if (phase == CsEvalPhase.Plan && (eval is not CsEvalPlanSuccessResult || applicationState != ExecutionApplicationState.NotApplied || string.IsNullOrWhiteSpace(planToken) || readPostcondition is not null)) throw new ArgumentException("eval.plan success must be notApplied, include a plan token, and contain only a plan result.");
        if (phase == CsEvalPhase.Call && (eval is not CsEvalCallSuccessResult || applicationState != ExecutionApplicationState.Applied || planToken is not null || readPostcondition is null)) throw new ArgumentException("eval.call success must be applied, omit a plan token, and contain a read postcondition.");
        if (phase == CsEvalPhase.Call) ValidateCallReadPostcondition(readPostcondition!);
        Phase = phase;
        ApplicationState = applicationState;
        Eval = eval;
        PlanToken = planToken;
        ReadPostcondition = readPostcondition;
    }

    [JsonInclude, JsonRequired] public UnityProjectIdentity Project { get; }
    [JsonInclude, JsonRequired] public CsEvalPhase Phase { get; }
    [JsonInclude, JsonRequired] public ExecutionApplicationState ApplicationState { get; }
    [JsonInclude, JsonRequired] public object Eval { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? PlanToken { get; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ExecutionReadPostcondition? ReadPostcondition { get; }

    private static void ValidateCallReadPostcondition (ExecutionReadPostcondition readPostcondition)
    {
        var requirements = readPostcondition.Requirements;
        if (requirements.Count != 3
            || requirements.Any(static requirement => requirement.ScenePath is not null)
            || !requirements.Select(static requirement => requirement.Surface).OrderBy(static surface => surface).SequenceEqual(
                new[]
                {
                    ExecutionReadPostconditionSurface.AssetSearch,
                    ExecutionReadPostconditionSurface.GuidPath,
                    ExecutionReadPostconditionSurface.SceneTreeLite,
                }))
        {
            throw new ArgumentException(
                "eval.call success must require global assetSearch, guidPath, and sceneTreeLite rereads.",
                nameof(readPostcondition));
        }
    }
}

/// <summary> Defines an eval failure response after the evaluation stage has started. </summary>
public sealed record IpcEvalErrorResponse
{
    /// <summary> Initializes one evaluation failure response. </summary>
    [JsonConstructor]
    public IpcEvalErrorResponse (UnityProjectIdentity project, CsEvalPhase phase, ExecutionApplicationState applicationState, CsEvalPartialErrorResult? eval, ExecutionReadPostcondition? readPostcondition)
    {
        Project = project ?? throw new ArgumentNullException(nameof(project));
        if (!TextVocabulary.IsDefined(phase)) throw new ArgumentOutOfRangeException(nameof(phase));
        if (!TextVocabulary.IsDefined(applicationState) || applicationState == ExecutionApplicationState.PartiallyApplied) throw new ArgumentOutOfRangeException(nameof(applicationState));
        if (phase == CsEvalPhase.Plan && applicationState != ExecutionApplicationState.NotApplied)
        {
            throw new ArgumentException("eval.plan failures are not applied.", nameof(applicationState));
        }

        if (phase == CsEvalPhase.Plan && readPostcondition is not null)
        {
            throw new ArgumentException("eval.plan failures cannot include a read postcondition.", nameof(readPostcondition));
        }

        if (phase == CsEvalPhase.Call
            && applicationState == ExecutionApplicationState.NotApplied
            && readPostcondition is not null)
        {
            throw new ArgumentException(
                "A proven pre-entry eval.call failure cannot include a read postcondition.",
                nameof(readPostcondition));
        }
        Phase = phase;
        ApplicationState = applicationState;
        Eval = eval;
        ReadPostcondition = readPostcondition;
    }

    [JsonInclude, JsonRequired] public UnityProjectIdentity Project { get; private init; }
    [JsonInclude, JsonRequired] public CsEvalPhase Phase { get; private init; }
    [JsonInclude, JsonRequired] public ExecutionApplicationState ApplicationState { get; private init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CsEvalPartialErrorResult? Eval { get; private init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public ExecutionReadPostcondition? ReadPostcondition { get; private init; }
}
