using MackySoft.Tests;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorExternalProcessRunnerTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunIgnoringExitCode_WhenProcessExitsNonZero_DoesNotThrow ()
    {
        var runner = new SupervisorExternalProcessRunner();
        var invocation = TestProcessInvocations.CreateNonZeroExit();

        await runner.RunIgnoringExitCodeAsync(
            invocation.FileName,
            invocation.Arguments,
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunIgnoringExitCode_WhenProcessCannotStart_DoesNotThrow ()
    {
        var runner = new SupervisorExternalProcessRunner();

        await runner.RunIgnoringExitCodeAsync(
            "definitely-missing-ucli-command",
            Array.Empty<string>(),
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task RunIgnoringExitCode_WhenCancellationIsRequested_ThrowsOperationCanceledException ()
    {
        var runner = new SupervisorExternalProcessRunner();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                runner.RunIgnoringExitCodeAsync(
                    "dotnet",
                    ["--info"],
                    cancellationTokenSource.Token).AsTask(),
                "Canceled supervisor external process run",
                AsyncWaitTimeout);
        });
    }
}
