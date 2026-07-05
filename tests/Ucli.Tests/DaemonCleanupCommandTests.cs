using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.UseCases.Cleanup;
using MackySoft.Ucli.Hosting.Cli.Daemon;
using MackySoft.Ucli.Tests.Hosting.Cli.Common.Execution;

namespace MackySoft.Ucli.Tests;

public sealed class DaemonCleanupCommandTests
{
    [Theory]
    [InlineData("skipped", 0, "unsafeInvalidSession")]
    [InlineData("skipped", 0, "uncertainReachability")]
    [InlineData("completed", 3, null)]
    [Trait("Size", "Small")]
    public async Task Cleanup_WithSuccessfulServiceResult_WritesCleanupPayload (
        string expectedCleanupStatus,
        int deletedLaunchAttemptCount,
        string? expectedSkipReason)
    {
        var output = expectedCleanupStatus switch
        {
            "skipped" => new DaemonCleanupExecutionOutput(
                CleanupStatus: DaemonCleanupStatus.Skipped,
                SkipReason: ParseCleanupSkipReason(expectedSkipReason),
                DeletedLaunchAttemptCount: deletedLaunchAttemptCount,
                TimeoutMilliseconds: 3000),
            "completed" => new DaemonCleanupExecutionOutput(
                CleanupStatus: DaemonCleanupStatus.Completed,
                SkipReason: DaemonCleanupSkipReason.None,
                DeletedLaunchAttemptCount: deletedLaunchAttemptCount,
                TimeoutMilliseconds: 3000),
            _ => throw new ArgumentOutOfRangeException(nameof(expectedCleanupStatus), expectedCleanupStatus, "Unsupported cleanup status."),
        };
        var service = new StubDaemonCleanupService(
            DaemonCleanupExecutionResult.Success(output));
        var command = new DaemonCleanupCommand(service, CommandResultTestWriter.Create());

        CommandExecutionState.Reset();
        var result = await CommandResultCapture.ExecuteAsync(() => command.CleanupAsync(
            projectPath: "/repo/UnityProject",
            timeout: "3000",
            cancellationToken: CancellationToken.None));

        Assert.Equal((int)CliExitCode.Success, result.ExitCode);
        using var outputJson = StdoutJsonParser.ParseSinglePrettyPrintedObject(result.StdOut);
        CommandResultAssert.HasSuccessEnvelope(
            outputJson.RootElement,
            UcliCommandNames.DaemonCleanup);
        CommandResultAssert.HasNoErrors(outputJson.RootElement);
        JsonAssert.For(outputJson.RootElement)
            .HasProperty("payload", payload => payload
                .HasString("cleanupStatus", expectedCleanupStatus)
                .HasInt32("deletedLaunchAttemptCount", deletedLaunchAttemptCount)
                .HasInt32("timeoutMilliseconds", 3000));
        var skipReasonElement = outputJson.RootElement.GetProperty("payload").GetProperty("skipReason");
        if (expectedSkipReason is null)
        {
            Assert.Equal(JsonValueKind.Null, skipReasonElement.ValueKind);
        }
        else
        {
            Assert.Equal(expectedSkipReason, skipReasonElement.GetString());
        }
    }

    private static DaemonCleanupSkipReason ParseCleanupSkipReason (string? expectedSkipReason)
    {
        return expectedSkipReason switch
        {
            "unsafeInvalidSession" => DaemonCleanupSkipReason.UnsafeInvalidSession,
            "uncertainReachability" => DaemonCleanupSkipReason.UncertainReachability,
            _ => throw new ArgumentOutOfRangeException(nameof(expectedSkipReason), expectedSkipReason, "Unsupported daemon cleanup skip reason."),
        };
    }

}
