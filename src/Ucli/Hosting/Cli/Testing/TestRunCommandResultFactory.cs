using MackySoft.Ucli.Application.Features.Testing.Run.Common.Contracts;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Hosting.Cli.Testing;

/// <summary> Creates command-level JSON results from test-run service results. </summary>
internal static class TestRunCommandResultFactory
{
    /// <summary> Creates one command-level JSON result from test-run service output. </summary>
    /// <param name="serviceResult"> The test-run service result. </param>
    /// <returns> The command result serialized to stdout. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="serviceResult" /> is <see langword="null" />. </exception>
    public static CommandResult Create (TestRunServiceResult serviceResult)
    {
        ArgumentNullException.ThrowIfNull(serviceResult);

        var payload = new
        {
            result = serviceResult.ResultValue,
            errorKind = serviceResult.ErrorKindValue,
            runId = serviceResult.RunId,
            artifactsDir = serviceResult.ArtifactsDir,
            summaryJsonPath = serviceResult.SummaryJsonPath,
        };

        if (serviceResult.ErrorKind is null)
        {
            return new CommandResult(
                ProtocolVersion: IpcProtocol.CurrentVersion,
                Command: UcliCommandNames.TestRun,
                Status: IpcProtocol.StatusOk,
                ExitCode: ApplicationOutcomeCliExitCodeMapper.ToExitCode(serviceResult.Outcome),
                Message: serviceResult.Message,
                Payload: payload,
                Errors: Array.Empty<CommandError>());
        }

        return CommandFailureProjector.Create(
            UcliCommandNames.TestRun,
            serviceResult.Message,
            payload,
            [
                serviceResult.Failure!,
            ]);
    }
}
