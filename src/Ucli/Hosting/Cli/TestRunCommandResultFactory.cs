using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.Testing.Run;

namespace MackySoft.Ucli.Hosting.Cli;

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
                ExitCode: serviceResult.ExitCode,
                Message: serviceResult.Message,
                Payload: payload,
                Errors: Array.Empty<CommandError>());
        }

        return new CommandResult(
            ProtocolVersion: IpcProtocol.CurrentVersion,
            Command: UcliCommandNames.TestRun,
            Status: IpcProtocol.StatusError,
            ExitCode: serviceResult.ExitCode,
            Message: serviceResult.Message,
            Payload: payload,
            Errors:
            [
                new CommandError(
                    ResolveErrorCode(serviceResult.ErrorCode),
                    serviceResult.Message,
                    null),
            ]);
    }

    /// <summary> Resolves one command error-code value with internal fallback. </summary>
    /// <param name="errorCode"> The source error code. </param>
    /// <returns> The source value when present; otherwise <c>INTERNAL_ERROR</c>. </returns>
    private static string ResolveErrorCode (string? errorCode)
    {
        return string.IsNullOrWhiteSpace(errorCode)
            ? IpcErrorCodes.InternalError
            : errorCode;
    }
}