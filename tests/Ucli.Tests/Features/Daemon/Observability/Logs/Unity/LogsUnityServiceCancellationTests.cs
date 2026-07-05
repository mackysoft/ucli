using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Logs.LogsUnityServiceTestSupport;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class LogsUnityServiceCancellationTests
{
    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenCancellationRequested_ThrowsOperationCanceledException ()
    {
        var resolver = CreateResolver();
        var service = CreateService(resolver, new RecordingUnityLogsClient(Array.Empty<UnityLogsClientReadResult>()));
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                service.ExecuteAsync(
                        new LogsUnityServiceRequest(
                            ProjectPath: "/tmp/unity-project",
                            Tail: null,
                            After: null,
                            Since: null,
                            Until: null,
                            Level: null,
                            Query: null,
                            QueryTarget: null,
                            Source: null,
                            StackTrace: null,
                            StackTraceMaxFrames: null,
                            StackTraceMaxChars: null,
                            Stream: false,
                            PollIntervalMilliseconds: null,
                            IdleTimeoutMilliseconds: null),
                        static (_, _, _) => ValueTask.CompletedTask,
                        cancellationTokenSource.Token)
                    .AsTask(),
                "Canceled unity logs execution",
                AsyncWaitTimeout);
        });
    }
}
