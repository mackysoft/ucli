using MackySoft.Ucli.Application.Features.Requests.Shared.Execution.OperationExecute;
using MackySoft.Ucli.Hosting.Cli.Requests;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.RefreshCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class RefreshCommandGoldenOutputTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WhenContractViolationExists_MatchesGolden ()
    {
        var failureResult = OperationExecuteResultFactory.Failure(
            RequestGuid,
            [
                CreateViolationOperationResult(),
            ],
            [
                ApplicationFailure.ContractViolation(
                    ContractViolationMessage,
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    "/opResults/0",
                    startupFailure: null),
            ],
            contractViolations:
            [
                CreateContractViolation(),
            ],
            readPostcondition: null,
            project: ProjectIdentityInfoTestFactory.Create(),
            postReadSource: null);
        var service = new RecordingRefreshService((_, _) => ValueTask.FromResult(failureResult));
        var command = new RefreshCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.RefreshAsync(
            projectPath: "/repo/UnityProject",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("refresh", "contract-violation.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeRequestIds());
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Refresh_WhenMayPersistContractViolationExists_MatchesGolden ()
    {
        var failureResult = OperationExecuteResultFactory.Failure(
            RequestGuid,
            [
                CreateViolationOperationResult(),
            ],
            [
                ApplicationFailure.ContractViolation(
                    ContractViolationMessage,
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    "/opResults/0",
                    startupFailure: null),
            ],
            contractViolations:
            [
                CreateContractViolation(
                    expectedFact: "assurance.mayPersist=false",
                    observedResult: "executionTrace.persisted=true"),
            ],
            readPostcondition: null,
            project: ProjectIdentityInfoTestFactory.Create(),
            postReadSource: null);
        var service = new RecordingRefreshService((_, _) => ValueTask.FromResult(failureResult));
        var command = new RefreshCommand(service, CommandResultTestWriter.Create());

        var result = await CommandResultCapture.ExecuteAsync(() => command.RefreshAsync(
            projectPath: "/repo/UnityProject",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.ToolError, result.ExitCode);
        JsonGoldenFileAssert.Matches(
            CliOutputGoldenFiles.GetPath("refresh", "contract-violation-may-persist.json"),
            result.StdOut,
            CliOutputGoldenFiles.NormalizeRequestIds());
    }
}
