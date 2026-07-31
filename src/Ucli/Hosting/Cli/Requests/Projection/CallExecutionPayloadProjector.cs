using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Requests.Call.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.Results;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Contracts.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests.Projection;

/// <summary> Projects call workflow output into the shared request-execution command payload shape. </summary>
internal static class CallExecutionPayloadProjector
{
    /// <summary> Gets the serializer contract used by successful <c>call</c> and <c>eval</c> payloads. </summary>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(CallSuccessCommandPayload));

    /// <summary> Gets the serializer contract used by failed <c>call</c> and <c>eval</c> payloads. </summary>
    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<CallErrorCommandPayload>();

    /// <summary> Creates the common error branch with no call-execution details. </summary>
    public static object CreateEmptyError ()
    {
        return CommandErrorPayload.Empty<CallErrorCommandPayload>();
    }

    /// <summary> Creates the successful command payload for a call-based workflow. </summary>
    /// <param name="output"> The call workflow output. </param>
    /// <returns> The actual command payload serialized by the CLI boundary. </returns>
    public static object CreateSuccess (CallExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new CallSuccessCommandPayload(
            output.RequestId,
            output.Project,
            output.OpResults,
            NullIfEmpty(output.ContractViolations),
            output.ReadPostcondition,
            output.PostReadSource,
            CreatePlan(output));
    }

    /// <summary> Creates the failed command payload for a call-based workflow. </summary>
    /// <param name="output"> The partial call workflow output, when execution reached that boundary. </param>
    /// <param name="failures"> The classified failures that may carry startup details. </param>
    /// <returns> The actual command payload serialized by the CLI boundary. </returns>
    public static object CreateError (
        CallExecutionOutput? output,
        IReadOnlyList<ApplicationFailure> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        var startupFailure = StartupFailureFinder.FindInFailures(failures);
        if (output == null && startupFailure == null)
        {
            return CreateEmptyError();
        }

        return CommandErrorPayload.Detailed(new CallErrorCommandPayload(
            output?.RequestId,
            output?.Project,
            output?.OpResults,
            NullIfEmpty(output?.ContractViolations),
            output?.ReadPostcondition,
            output?.PostReadSource,
            output is null ? null : CreatePlan(output),
            startupFailure?.Startup,
            startupFailure?.Diagnosis,
            startupFailure?.RetryDisposition,
            startupFailure?.SafeToRetryImmediately));
    }

    private static IReadOnlyList<T>? NullIfEmpty<T> (IReadOnlyList<T>? values)
    {
        return values is null || values.Count == 0 ? null : values;
    }

    private static CallPlanCommandPayload? CreatePlan (CallExecutionOutput output)
    {
        return output.Plan is null
            ? null
            : new CallPlanCommandPayload(
                output.RequestId,
                output.Project,
                output.Plan.OpResults,
                NullIfEmpty(output.Plan.ContractViolations),
                string.IsNullOrWhiteSpace(output.Plan.PlanToken) ? null : output.Plan.PlanToken);
    }

    private sealed record CallSuccessCommandPayload (
        Guid RequestId,
        ProjectIdentityInfo Project,
        IReadOnlyList<OperationExecutionOperationResult> OpResults,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<OperationExecutionContractViolation>? ContractViolations,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ExecutionReadPostcondition? ReadPostcondition,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        OperationExecutionPostReadSource? PostReadSource,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        CallPlanCommandPayload? Plan);

    private sealed record CallErrorCommandPayload (
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        Guid? RequestId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ProjectIdentityInfo? Project,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<OperationExecutionOperationResult>? OpResults,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<OperationExecutionContractViolation>? ContractViolations,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ExecutionReadPostcondition? ReadPostcondition,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        OperationExecutionPostReadSource? PostReadSource,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        CallPlanCommandPayload? Plan,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupObservationOutput? Startup,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonDiagnosisOutput? Diagnosis,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupRetryDisposition? RetryDisposition,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        bool? SafeToRetryImmediately)
        : CommandErrorPayload<CallErrorCommandPayload>;

    private sealed record CallPlanCommandPayload (
        Guid RequestId,
        ProjectIdentityInfo Project,
        IReadOnlyList<OperationExecutionOperationResult> OpResults,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<OperationExecutionContractViolation>? ContractViolations,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? PlanToken);
}
