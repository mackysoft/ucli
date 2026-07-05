using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using static MackySoft.Ucli.Tests.Logs.LogsUnityServiceTestSupport;

namespace MackySoft.Ucli.Tests.Logs;

public sealed class LogsUnityServiceValidationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStackTraceModeIsInvalid_ReturnsInvalidArgument ()
    {
        var resolver = CreateResolver();
        var service = CreateService(resolver, new RecordingUnityLogsClient(Array.Empty<UnityLogsClientReadResult>()));

        var result = await service.ExecuteAsync(
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
                StackTrace: "unsupported",
                StackTraceMaxFrames: null,
                StackTraceMaxChars: null,
                Stream: false,
                PollIntervalMilliseconds: null,
                IdleTimeoutMilliseconds: null),
            static (_, _, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        LogsUnityServiceAssert.InvalidStackTraceRejectedBeforeContextResolution(
            result,
            resolver);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Execute_WhenStackTraceModeIsNone_IgnoresStackTraceLimitValidation ()
    {
        var resolver = CreateResolver();
        var unityLogsClient = new RecordingUnityLogsClient(
            [
                UnityLogsClientReadResult.Success(CreatePayload(
                    events: Array.Empty<IpcUnityLogEvent>(),
                    nextCursor: "stream-1:1")),
            ]);
        var service = CreateService(resolver, unityLogsClient);

        var result = await service.ExecuteAsync(
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
                StackTrace: "none",
                StackTraceMaxFrames: 1024,
                StackTraceMaxChars: 1,
                Stream: false,
                PollIntervalMilliseconds: null,
                IdleTimeoutMilliseconds: null),
            static (_, _, _) => ValueTask.CompletedTask,
            CancellationToken.None);

        Assert.True(result.IsSuccess, result.Error?.Message);
        UnityLogsClientAssert.SingleReadWithStackTraceNoneAndNoLimits(unityLogsClient);
    }
}
