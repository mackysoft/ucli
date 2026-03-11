using MackySoft.Ucli.Supervisor;

namespace MackySoft.Ucli.Tests.Supervisor;

public sealed class SupervisorExternalProcessRunnerTests
{
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
            await runner.RunIgnoringExitCode(
                "dotnet",
                ["--info"],
                cancellationTokenSource.Token);
        });
    }
}