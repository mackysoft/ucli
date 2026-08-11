using MackySoft.Ucli.Application.Features.Assurance.Verify.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.VerifyCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class VerifyCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Verify_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new RecordingVerifyService((_, _, _) => ValueTask.FromResult<VerifyExecutionResult>(VerifyExecutionResult.Completed(CreateOutput(Verdict.Pass))));
        var command = new VerifyCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System);
        var profilePath = FilePathReference.Parse("profiles/verify.json");
        var fromPath = FilePathReference.Parse("artifacts/call-result.json");
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.VerifyAsync(
            profile: null,
            profilePath: profilePath,
            from: fromPath,
            projectPath: AbsolutePath.Parse(ProjectPathTestValues.RepositoryUnityProject),
            mode: "daemon",
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        VerifyCommandAssert.SucceededWithDispatchedInput(
            result,
            service,
            cancellationTokenSource.Token,
            expectedProfile: null,
            expectedProfilePath: profilePath,
            expectedFromPath: fromPath,
            expectedProjectPath: ProjectPathTestValues.RepositoryUnityProject,
            expectedMode: UnityExecutionMode.Daemon,
            expectedTimeoutMilliseconds: 1234);
    }
}
