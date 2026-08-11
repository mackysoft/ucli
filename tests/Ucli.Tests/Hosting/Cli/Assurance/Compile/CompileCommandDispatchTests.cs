using MackySoft.Ucli.Application.Features.Assurance.Compile.Contracts;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Hosting.Cli.Assurance;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;
using static MackySoft.Ucli.Tests.CompileCommandTestData;

namespace MackySoft.Ucli.Tests;

public sealed class CompileCommandDispatchTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Compile_MapsOptionsToServiceInputAndCancellationToken ()
    {
        var service = new RecordingCompileService((_, _, _) => ValueTask.FromResult<CompileExecutionResult>(CompileExecutionResult.Completed(CreateOutput())));
        var invocationFactory = new RecordingLifecycleExecutionStartInvocationFactory();
        var command = new CompileCommand(service, CommandResultTestWriter.Create(), CliStreamEntryWriterFactoryTestFixture.System, invocationFactory);
        using var cancellationTokenSource = new CancellationTokenSource();

        var result = await CommandResultCapture.ExecuteAsync(() => command.CompileAsync(
            projectPath: AbsolutePath.Parse(ProjectPathTestValues.RepositoryUnityProject),
            mode: "daemon",
            timeout: "1234",
            cancellationToken: cancellationTokenSource.Token));

        CompileCommandAssert.SucceededWithDispatchedInput(
            result,
            invocationFactory,
            cancellationTokenSource.Token,
            ProjectPathTestValues.RepositoryUnityProject,
            UnityExecutionMode.Daemon,
            expectedTimeoutMilliseconds: 1234);
        Assert.Equal(1, invocationFactory.DisposeCount);
    }
}
