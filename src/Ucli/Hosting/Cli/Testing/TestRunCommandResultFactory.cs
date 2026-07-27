using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Testing;

/// <summary> Creates command-level JSON results from test-run service results. </summary>
internal static class TestRunCommandResultFactory
{
    /// <summary> Gets the serializer contract used by successful <c>test run</c> payloads. </summary>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(TestRunSuccessCommandPayload));

    /// <summary> Gets the serializer contract used by failed <c>test run</c> payloads. </summary>
    public static JsonTypeInfo ErrorPayloadTypeInfo { get; } =
        CommandErrorPayload.TypeInfo<TestRunErrorCommandPayload>();

    public static object CreateEmptyErrorPayload ()
    {
        return CommandErrorPayload.Empty<TestRunErrorCommandPayload>();
    }

    /// <summary> Creates one command-level JSON result from test-run service output. </summary>
    /// <param name="serviceResult"> The test-run service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceResult" /> is <see langword="null" />. </exception>
    public static CommandResult Create (TestRunServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        if (serviceResult.ErrorKind is null)
        {
            return new CommandResult(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                Command: UcliCommandNames.TestRun,
                Status: CommandResultStatus.Ok,
                ExitCode: ApplicationOutcomeCliExitCodeMapper.ToExitCode(serviceResult.Outcome),
                Message: serviceResult.Message,
                Payload: new TestRunSuccessCommandPayload(
                    serviceResult.Result!.Value,
                    serviceResult.RunId!.Value,
                    serviceResult.ArtifactsDir!.Value,
                    serviceResult.SummaryJsonPath!.Value),
                Errors: Array.Empty<CommandError>());
        }

        var startupFailure = serviceResult.StartupFailure;
        return CommandFailureProjector.Create(
            UcliCommandNames.TestRun,
            serviceResult.Message,
            CommandErrorPayload.Detailed(new TestRunErrorCommandPayload(
                serviceResult.ErrorKind.Value,
                serviceResult.RunId,
                serviceResult.ArtifactsDir?.Value,
                serviceResult.SummaryJsonPath?.Value,
                startupFailure?.Startup,
                startupFailure?.Diagnosis,
                startupFailure?.RetryDisposition,
                startupFailure?.SafeToRetryImmediately)),
            [
                serviceResult.Failure!,
            ]);
    }

    private sealed record TestRunSuccessCommandPayload (
        TestRunResultKind Result,
        Guid RunId,
        string ArtifactsDir,
        string SummaryJsonPath);

    private sealed record TestRunErrorCommandPayload (
        TestRunErrorKind ErrorKind,
        Guid? RunId,
        string? ArtifactsDir,
        string? SummaryJsonPath,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupObservationOutput? Startup,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonDiagnosisOutput? Diagnosis,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        DaemonStartupRetryDisposition? RetryDisposition,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        bool? SafeToRetryImmediately)
        : CommandErrorPayload<TestRunErrorCommandPayload>;
}
