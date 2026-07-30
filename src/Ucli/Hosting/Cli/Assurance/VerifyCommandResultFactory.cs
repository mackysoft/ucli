using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Verify.Payload;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Creates command-level JSON results from <c>verify</c> execution results. </summary>
internal static class VerifyCommandResultFactory
{
    /// <summary> Gets the serializer contract used by successful <c>verify</c> payloads. </summary>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(VerifyExecutionOutput));

    /// <summary> Gets the serializer contract used by failed <c>verify</c> payloads. </summary>
    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<VerifyFailureCommandPayload>();

    public static object CreateEmptyErrorPayload ()
    {
        return CommandErrorPayload.Empty<VerifyFailureCommandPayload>();
    }

    /// <summary> Creates one command result for <c>verify</c>. </summary>
    public static CommandResult Create (VerifyExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        return executionResult switch
        {
            VerifyExecutionResult.CompletedResult completed => CreateSuccess(completed),
            VerifyExecutionResult.FailedResult failed => CreateFailure(failed),
            _ => throw new ArgumentOutOfRangeException(
                nameof(executionResult),
                executionResult.GetType(),
                "Verify execution result variant is unsupported."),
        };
    }

    /// <summary> Creates one command result for <c>verify</c> from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(VerifyExecutionResult.Failed(error, project: null));
    }

    private static CommandResult CreateSuccess (VerifyExecutionResult.CompletedResult executionResult)
    {
        return CommandResult.CompletedWithVerdict(
            UcliCommandNames.Verify,
            executionResult.Message,
            executionResult.Output);
    }

    private static CommandResult CreateFailure (VerifyExecutionResult.FailedResult executionResult)
    {
        var startupFailure = StartupFailureFinder.FindInFailures([executionResult.Failure]);
        return CommandFailureProjector.Create(
            UcliCommandNames.Verify,
            executionResult.Message,
            CommandErrorPayload.Detailed(new VerifyFailureCommandPayload(
                executionResult.Project,
                startupFailure?.Startup,
                startupFailure?.Diagnosis,
                startupFailure?.RetryDisposition,
                startupFailure?.SafeToRetryImmediately)),
            [executionResult.Failure]);
    }

    private sealed record VerifyFailureCommandPayload (
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        ProjectIdentityInfo? Project,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupObservationOutput? Startup,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonDiagnosisOutput? Diagnosis,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupRetryDisposition? RetryDisposition,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        bool? SafeToRetryImmediately)
        : CommandErrorPayload<VerifyFailureCommandPayload>;
}
