using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Requests.Plan.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution;
using MackySoft.Ucli.Application.Shared.Execution.ReadIndex;
using MackySoft.Ucli.Application.Shared.Execution.Results;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Creates command-level JSON results from <c>plan</c> service results. </summary>
internal static class PlanCommandResultFactory
{
    /// <summary> Gets the serializer contract used by successful <c>plan</c> payloads. </summary>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(PlanSuccessCommandPayload));

    /// <summary> Gets the serializer contract used by failed <c>plan</c> payloads. </summary>
    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<PlanErrorCommandPayload>();

    public static object CreateEmptyErrorPayload ()
    {
        return CommandErrorPayload.Empty<PlanErrorCommandPayload>();
    }

    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return CommandFailureProjector.Create(
            UcliCommandNames.Plan,
            ApplicationFailure.FromExecutionError(error),
            CreateEmptyErrorPayload());
    }

    /// <summary> Creates one command result for <c>plan</c>. </summary>
    /// <param name="serviceResult"> The service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    public static CommandResult Create (PlanServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        if (serviceResult.IsSuccess)
        {
            return CommandResult.Success(
                command: UcliCommandNames.Plan,
                message: serviceResult.Message,
                payload: CreateSuccessPayload(serviceResult.Output!));
        }

        return CommandFailureProjector.Create(
            UcliCommandNames.Plan,
            serviceResult.Message,
            CreateErrorPayload(serviceResult.Output, serviceResult.Errors),
            serviceResult.Errors);
    }

    private static PlanSuccessCommandPayload CreateSuccessPayload (
        PlanExecutionOutput output)
    {
        return new PlanSuccessCommandPayload(
            output.RequestId,
            output.Project,
            output.OpResults,
            output.ContractViolations.Count == 0
                ? null
                : output.ContractViolations,
            output.ReadIndex,
            string.IsNullOrWhiteSpace(output.PlanToken) ? null : output.PlanToken);
    }

    private static object CreateErrorPayload (
        PlanExecutionOutput? output,
        IReadOnlyList<ApplicationFailure> failures)
    {
        var startupFailure = StartupFailureFinder.FindInFailures(failures);
        if (output == null && startupFailure == null)
        {
            return CreateEmptyErrorPayload();
        }

        return CommandErrorPayload.Detailed(new PlanErrorCommandPayload(
            output?.RequestId,
            output?.Project,
            output?.OpResults,
            output is null || output.ContractViolations.Count == 0
                ? null
                : output.ContractViolations,
            output?.ReadIndex,
            string.IsNullOrWhiteSpace(output?.PlanToken) ? null : output.PlanToken,
            startupFailure?.Startup,
            startupFailure?.Diagnosis,
            startupFailure?.RetryDisposition,
            startupFailure?.SafeToRetryImmediately));
    }

    private sealed record PlanSuccessCommandPayload (
        Guid RequestId,
        ProjectIdentityInfo Project,
        IReadOnlyList<OperationExecutionOperationResult> OpResults,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<OperationExecutionContractViolation>? ContractViolations,
        ReadIndexInfo ReadIndex,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? PlanToken);

    private sealed record PlanErrorCommandPayload (
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        Guid? RequestId,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ProjectIdentityInfo? Project,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<OperationExecutionOperationResult>? OpResults,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<OperationExecutionContractViolation>? ContractViolations,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ReadIndexInfo? ReadIndex,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? PlanToken,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupObservationOutput? Startup,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonDiagnosisOutput? Diagnosis,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupRetryDisposition? RetryDisposition,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        bool? SafeToRetryImmediately)
        : CommandErrorPayload<PlanErrorCommandPayload>;
}
