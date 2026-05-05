using MackySoft.Tests;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorExternalProcessRunnerTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task RunIgnoringExitCode_WhenProcessExitsNonZero_DoesNotThrow ()
    {
        var runner = new SupervisorExternalProcessRunner();

        await runner.RunIgnoringExitCode(
            "dotnet",
            ["--definitely-invalid-supervisor-switch"],
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RunIgnoringExitCode_WhenProcessCannotStart_DoesNotThrow ()
    {
        var runner = new SupervisorExternalProcessRunner();

        await runner.RunIgnoringExitCode(
            "definitely-missing-ucli-command",
            Array.Empty<string>(),
            CancellationToken.None);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task RunIgnoringExitCode_WhenCancellationIsRequested_ThrowsOperationCanceledException ()
    {
        var runner = new SupervisorExternalProcessRunner();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                runner.RunIgnoringExitCode(
                    "dotnet",
                    ["--info"],
                    cancellationTokenSource.Token).AsTask(),
                "Canceled supervisor external process run",
                AsyncWaitTimeout);
        });
    }
}
