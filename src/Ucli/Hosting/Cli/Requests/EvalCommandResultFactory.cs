using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Eval;
using MackySoft.Ucli.Contracts.Execution;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Projects;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from <c>eval</c> service results. </summary>
internal static class EvalCommandResultFactory
{
    private const string SuccessMessage = "uCLI eval completed.";

    /// <summary> Creates one command result for <c>eval</c>. </summary>
    /// <param name="serviceResult"> The dedicated eval workflow result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(EvalSuccessCommandPayload));

    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<EvalErrorCommandPayload>();

    public static object CreateEmptyErrorPayload () =>
        CommandErrorPayload.Empty<EvalErrorCommandPayload>();

    public static CommandResult Create (EvalServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        if (serviceResult.IsSuccess)
        {
            var plan = serviceResult.Plan!;
            var call = serviceResult.Call!;
            return CommandResult.Success(
                command: UcliCommandNames.Eval,
                message: SuccessMessage,
                payload: new EvalSuccessCommandPayload(
                    serviceResult.RequestId!.Value,
                    call.Project,
                    EvalSuccessApplicationState.Applied,
                    (CsEvalCallSuccessResult)call.Eval,
                    CreatePlanPayload(serviceResult.RequestId!.Value, plan),
                    call.ReadPostcondition!));
        }

        return CommandFailureProjector.Create(
            UcliCommandNames.Eval,
            ApplicationFailure.FromExecutionError(serviceResult.Error!),
            CreateErrorPayload(serviceResult));
    }

    private static object CreateErrorPayload (EvalServiceResult result)
    {
        if (result.RequestId is null && result.Plan is null)
        {
            return CreateEmptyErrorPayload();
        }

        var observedError = result.ErrorResponse;
        var phase = observedError?.Phase ?? (result.Plan is null ? CsEvalPhase.Plan : CsEvalPhase.Call);
        var applicationState = observedError?.ApplicationState
            ?? (result.CallWasSent
                ? ExecutionApplicationState.Indeterminate
                : result.Plan?.ApplicationState ?? ExecutionApplicationState.NotApplied);
        if (result.RequestId is not { } requestId
            || (observedError?.Project ?? result.Project) is not { } project)
        {
            return CreateEmptyErrorPayload();
        }

        return CommandErrorPayload.Detailed(new EvalErrorCommandPayload(
            requestId,
            project,
            phase,
            ToErrorApplicationState(applicationState),
            result.Plan is null || result.RequestId is null ? null : CreatePlanPayload(result.RequestId.Value, result.Plan),
            Eval: observedError?.Eval,
            ReadPostcondition: observedError?.ReadPostcondition));
    }

    private sealed record EvalSuccessCommandPayload (
        Guid RequestId,
        UnityProjectIdentity Project,
        EvalSuccessApplicationState ApplicationState,
        CsEvalCallSuccessResult Eval,
        EvalPlanCommandPayload Plan,
        ExecutionReadPostcondition ReadPostcondition);

    private sealed record EvalErrorCommandPayload (
        Guid RequestId,
        UnityProjectIdentity Project,
        CsEvalPhase Phase,
        EvalErrorApplicationState ApplicationState,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        EvalPlanCommandPayload? Plan,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        CsEvalPartialErrorResult? Eval,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ExecutionReadPostcondition? ReadPostcondition)
        : CommandErrorPayload<EvalErrorCommandPayload>;

    private static EvalPlanCommandPayload CreatePlanPayload (Guid requestId, IpcEvalResponse response)
    {
        return new EvalPlanCommandPayload(
            response.Project,
            requestId,
            EvalPlanApplicationState.NotApplied,
            (CsEvalPlanSuccessResult)response.Eval,
            response.PlanToken!);
    }

    private sealed record EvalPlanCommandPayload (
        UnityProjectIdentity Project,
        Guid RequestId,
        EvalPlanApplicationState ApplicationState,
        CsEvalPlanSuccessResult Eval,
        string PlanToken);

    private static EvalErrorApplicationState ToErrorApplicationState (ExecutionApplicationState state) => state switch
    {
        ExecutionApplicationState.Applied => EvalErrorApplicationState.Applied,
        ExecutionApplicationState.Indeterminate => EvalErrorApplicationState.Indeterminate,
        ExecutionApplicationState.NotApplied => EvalErrorApplicationState.NotApplied,
        ExecutionApplicationState.Unknown => EvalErrorApplicationState.Unknown,
        _ => throw new ArgumentOutOfRangeException(nameof(state)),
    };

    [VocabularyDefinition]
    private enum EvalSuccessApplicationState { [VocabularyText("applied")] Applied = 1 }

    [VocabularyDefinition]
    private enum EvalPlanApplicationState { [VocabularyText("notApplied")] NotApplied = 1 }

    [VocabularyDefinition]
    private enum EvalErrorApplicationState
    {
        [VocabularyText("applied")] Applied = 1,
        [VocabularyText("indeterminate")] Indeterminate = 2,
        [VocabularyText("notApplied")] NotApplied = 3,
        [VocabularyText("unknown")] Unknown = 4,
    }
}
