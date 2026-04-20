using MackySoft.Tests;
using MackySoft.Ucli.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Ipc;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Features.Daemon.Supervisor.Bootstrap;
using MackySoft.Ucli.Features.Daemon.Supervisor.Client;
using MackySoft.Ucli.Features.Daemon.Supervisor.Gateway;
using MackySoft.Ucli.Features.Daemon.Supervisor.Host;
using MackySoft.Ucli.Features.Daemon.Supervisor.Launch;
using MackySoft.Ucli.Features.Daemon.Supervisor.Transport;
using MackySoft.Ucli.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Features.Daemon.UseCases.Inventory;
using MackySoft.Ucli.Features.Daemon.UseCases.Start;
using MackySoft.Ucli.Features.Daemon.UseCases.Status;
using MackySoft.Ucli.Features.Daemon.UseCases.Stop;

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