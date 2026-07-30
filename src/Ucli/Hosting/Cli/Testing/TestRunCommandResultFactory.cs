using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Testing;

/// <summary> Creates command-level JSON results from test-run service results. </summary>
internal static class TestRunCommandResultFactory
{
    /// <summary> Gets the serializer contract used by successful <c>test run</c> payloads. </summary>
    public static JsonTypeInfo SuccessPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(TestRunCompletedCommandPayload));

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

        return serviceResult switch
        {
            TestRunCompletedServiceResult completed => CommandResult.CompletedWithVerdict(
                UcliCommandNames.TestRun,
                completed.Message,
                CreateCompletedPayload(completed)),
            TestRunCommandErrorServiceResult commandError => CommandFailureProjector.Create(
                UcliCommandNames.TestRun,
                commandError.Message,
                CommandErrorPayload.Detailed(new TestRunErrorCommandPayload(
                    commandError.ErrorKind,
                    commandError is TestRunAfterCreationCommandErrorServiceResult afterCreation
                        ? new TestRunErrorRunContext(
                            afterCreation.RunId,
                            afterCreation.ArtifactsDir.Value)
                        : null,
                    CreateStartupFailureDetail(commandError.PrimaryFailure.StartupFailure))),
                commandError.Failures),
            _ => throw new ArgumentOutOfRangeException(
                nameof(serviceResult),
                serviceResult.GetType(),
                "Test Run service result type must have an explicit CLI projection."),
        };
    }

    private static TestRunCompletedCommandPayload CreateCompletedPayload (
        TestRunCompletedServiceResult completed)
    {
        return new TestRunCompletedCommandPayload(
            completed.Verdict,
            completed.RunId,
            completed.ArtifactsDir.Value,
            completed.SummaryJsonPath.Value);
    }

    private static TestRunStartupFailureCommandDetail? CreateStartupFailureDetail (
        StartupFailureDetail? startupFailure)
    {
        return startupFailure is null
            ? null
            : new TestRunStartupFailureCommandDetail(
                startupFailure.Startup,
                startupFailure.Diagnosis,
                startupFailure.RetryDisposition,
                startupFailure.SafeToRetryImmediately);
    }

    private sealed record TestRunErrorCommandPayload (
        TestRunErrorKind ErrorKind,
        TestRunErrorRunContext? Run,
        TestRunStartupFailureCommandDetail? StartupFailure)
        : CommandErrorPayload<TestRunErrorCommandPayload>;

    private sealed record TestRunErrorRunContext (
        Guid RunId,
        string ArtifactsDir);

    private sealed record TestRunStartupFailureCommandDetail (
        DaemonStartupObservationOutput? Startup,
        DaemonDiagnosisOutput? Diagnosis,
        DaemonStartupRetryDisposition RetryDisposition,
        bool SafeToRetryImmediately);
}
