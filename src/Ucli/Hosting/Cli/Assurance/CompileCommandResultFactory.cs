using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Features.Assurance.Compile.Payload;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Assurance;

/// <summary> Creates command-level JSON results from <c>compile</c> execution results. </summary>
internal static class CompileCommandResultFactory
{
    /// <summary> Gets the serializer contract used by successful <c>compile</c> payloads. </summary>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(CompileExecutionOutput));

    /// <summary> Gets the serializer contract used by failed <c>compile</c> payloads. </summary>
    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<CompileFailureCommandPayload>();

    public static object CreateEmptyErrorPayload ()
    {
        return CommandErrorPayload.Empty<CompileFailureCommandPayload>();
    }

    /// <summary> Creates one command result for <c>compile</c>. </summary>
    public static CommandResult Create (CompileExecutionResult executionResult)
    {
        ArgumentNullException.ThrowIfNull(executionResult);

        if (executionResult.IsSuccess)
        {
            return CreateSuccess(executionResult);
        }

        var startupFailure = StartupFailureFinder.FindInFailures(executionResult.Errors);
        return CommandFailureProjector.Create(
            UcliCommandNames.Compile,
            executionResult.Message,
            CommandErrorPayload.Detailed(new CompileFailureCommandPayload(
                executionResult.Project,
                startupFailure?.Startup,
                startupFailure?.Diagnosis,
                startupFailure?.RetryDisposition,
                startupFailure?.SafeToRetryImmediately)),
            executionResult.Errors);
    }

    /// <summary> Creates one command result for <c>compile</c> from a normalized execution error. </summary>
    public static CommandResult CreateExecutionError (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Create(CompileExecutionResult.Failure(error));
    }

    private static CommandResult CreateSuccess (CompileExecutionResult executionResult)
    {
        var output = executionResult.Output!;
        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: UcliCommandNames.Compile,
            Status: CommandResultStatus.Ok,
            ExitCode: output.Verdict == AssuranceVerdict.Pass
                ? (int)CliExitCode.Success
                : 1,
            Message: executionResult.Message,
            Payload: output,
            Errors: []);
    }

    private sealed record CompileFailureCommandPayload (
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
        : CommandErrorPayload<CompileFailureCommandPayload>;
}
