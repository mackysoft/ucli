using MackySoft.Tests;
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
            RequestId,
            [
                CreateViolationOperationResult(),
            ],
            [
                ApplicationFailure.FromCode(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    ContractViolationMessage,
                    "refresh"),
            ],
            ContractViolationMessage,
            contractViolations:
            [
                CreateContractViolation(),
            ],
            project: ProjectIdentityInfoTestFactory.Create());
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
            RequestId,
            [
                CreateViolationOperationResult(),
            ],
            [
                ApplicationFailure.FromCode(
                    ExecuteRequestErrorCodes.OperationContractViolation,
                    ContractViolationMessage,
                    "refresh"),
            ],
            ContractViolationMessage,
            contractViolations:
            [
                CreateContractViolation(
                    expectedFact: "assurance.mayPersist=false",
                    observedResult: "executionTrace.persisted=true"),
            ],
            project: ProjectIdentityInfoTestFactory.Create());
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
