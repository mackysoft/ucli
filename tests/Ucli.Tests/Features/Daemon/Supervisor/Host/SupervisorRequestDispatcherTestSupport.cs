using System.Text.Json;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Compensation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;
using MackySoft.Ucli.Tests.Helpers.Ipc;

namespace MackySoft.Ucli.Tests.Supervisor;

internal static class SupervisorRequestDispatcherTestSupport
{
    private static readonly DateTimeOffset DefaultUtcNow = new(2026, 03, 11, 0, 0, 0, TimeSpan.Zero);

    public static SupervisorRequestDispatcher CreateDispatcher (
        RecordingDaemonStartOperation? startOperation = null,
        TimeProvider? timeProvider = null,
        RecordingDaemonStopOperation? stopOperation = null)
    {
        var effectiveTimeProvider = timeProvider ?? FixedUtcTimeProvider.Instance;
        var activityTracker = new SupervisorActivityTracker(effectiveTimeProvider);
        var diagnosisStore = new RecordingDaemonDiagnosisStore();
        var runtimeLogger = new SupervisorRuntimeLogger();
        var coordinator = new SupervisorProjectCoordinator(
            startOperation ?? new RecordingDaemonStartOperation(),
            stopOperation ?? new RecordingDaemonStopOperation(),
            new RecordingDaemonPingClient(),
            new DaemonReachabilityClassifier(),
            new SupervisorStabilityVerifier(
                new RecordingDaemonPingClient(),
                new SupervisorDiagnosisWriter(diagnosisStore),
                new DaemonCompensationOperationOwner(),
                effectiveTimeProvider),
            new SupervisorExitHandler(
                new RecordingDaemonSessionStore(),
                new RecordingDaemonArtifactCleaner(),
                new SupervisorDiagnosisWriter(diagnosisStore),
                runtimeLogger,
                effectiveTimeProvider),
            runtimeLogger,
            effectiveTimeProvider);
        return new SupervisorRequestDispatcher(
            activityTracker,
            coordinator,
            effectiveTimeProvider);
    }

    public static DateTimeOffset CreateEnsureRunningDeadline (int timeoutMilliseconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(timeoutMilliseconds);
        return DefaultUtcNow.AddMilliseconds(timeoutMilliseconds);
    }

    public static SupervisorRuntimeContext CreateRuntimeContext ()
    {
        return new SupervisorRuntimeContext(
            StorageRoot: ResolvedUnityProjectContextTestFactory.RepositoryRoot,
            Manifest: new SupervisorInstanceManifest(
                processId: 1234,
                sessionToken: IpcSessionTokenTestFactory.Create("supervisor-session-token"),
                endpoint: new IpcEndpoint(
                    IpcTransportKind.UnixDomainSocket,
                    "/tmp/ucli-supervisor-test.sock"),
                issuedAtUtc: new DateTimeOffset(2026, 03, 11, 0, 0, 0, TimeSpan.Zero)));
    }

    public static DaemonStartupObservation CreateStartupObservation ()
    {
        return new DaemonStartupObservation(
            StartupStatus: DaemonStartupStatus.Blocked,
            StartupBlockingReason: DaemonStartupBlockingReason.Compile,
            LaunchAttemptId: null,
            ProcessAction: DaemonStartupProcessAction.Kept,
            RetryDisposition: DaemonStartupRetryDisposition.RetryAfterFix,
            EditorMode: null,
            OwnerKind: null,
            CanShutdownProcess: null,
            ProcessId: null,
            StartedAtUtc: null,
            ElapsedMilliseconds: null,
            ArtifactPath: null);
    }

    public static async Task<IpcResponse> SendRequestAsync (
        SupervisorRequestDispatcher dispatcher,
        SupervisorRuntimeContext runtimeContext,
        IpcRequestEnvelope request)
    {
        return await SendFramedRequestAsync(dispatcher, runtimeContext, request).ConfigureAwait(false);
    }

    public static async Task<IpcResponse> SendRawJsonRequestAsync (
        SupervisorRequestDispatcher dispatcher,
        SupervisorRuntimeContext runtimeContext,
        JsonElement request)
    {
        return await SendFramedRequestAsync(dispatcher, runtimeContext, request).ConfigureAwait(false);
    }

    public static async Task<IpcResponse> SendRequestWithCallerDisconnectAsync (
        SupervisorRequestDispatcher dispatcher,
        SupervisorRuntimeContext runtimeContext,
        IpcRequestEnvelope request)
    {
        using var stream = new CallerDisconnectingMemoryStream();
        await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default)
            .ConfigureAwait(false);
        var requestLength = stream.Length;
        stream.Position = 0;

        await dispatcher.HandleConnectionAsync(
                stream,
                runtimeContext,
                SupervisorConstants.InitialFrameReadTimeout,
                CancellationToken.None)
            .ConfigureAwait(false);

        stream.Position = requestLength;
        return await IpcFrameCodec.ReadModelAsync<IpcResponse>(
                stream,
                IpcJsonSerializerOptions.Default)
            .ConfigureAwait(false);
    }

    public static async Task<IReadOnlyList<IpcStreamFrame>> SendStreamingRequestAsync (
        SupervisorRequestDispatcher dispatcher,
        SupervisorRuntimeContext runtimeContext,
        IpcRequestEnvelope request)
    {
        using var stream = new DuplexMemoryStream(await CreateRequestFrameBytesAsync(request).ConfigureAwait(false));

        await dispatcher.HandleConnectionAsync(
                stream,
                runtimeContext,
                SupervisorConstants.InitialFrameReadTimeout,
                CancellationToken.None)
            .ConfigureAwait(false);

        using var outputStream = new MemoryStream(stream.GetWrittenBytes());
        var frames = new List<IpcStreamFrame>();
        while (true)
        {
            var frame = await IpcFrameCodec.ReadModelAsync<IpcStreamFrame>(
                    outputStream,
                    IpcJsonSerializerOptions.Default)
                .ConfigureAwait(false);
            frames.Add(frame);
            if (frame.Kind == IpcStreamFrameKind.Terminal)
            {
                return frames;
            }
        }
    }

    public static async Task<IReadOnlyList<IpcStreamFrame>> SendStreamingRequestWithTransientWriteFailureAsync (
        SupervisorRequestDispatcher dispatcher,
        SupervisorRuntimeContext runtimeContext,
        IpcRequestEnvelope request)
    {
        using var stream = new DuplexMemoryStream(await CreateRequestFrameBytesAsync(request).ConfigureAwait(false))
        {
            FailWriteCount = 1,
        };

        await dispatcher.HandleConnectionAsync(
                stream,
                runtimeContext,
                SupervisorConstants.InitialFrameReadTimeout,
                CancellationToken.None)
            .ConfigureAwait(false);

        using var outputStream = new MemoryStream(stream.GetWrittenBytes());
        var frames = new List<IpcStreamFrame>();
        while (outputStream.Position < outputStream.Length)
        {
            frames.Add(await IpcFrameCodec.ReadModelAsync<IpcStreamFrame>(
                    outputStream,
                    IpcJsonSerializerOptions.Default)
                .ConfigureAwait(false));
        }

        return frames;
    }

    private static async Task<IpcResponse> SendFramedRequestAsync<TRequest> (
        SupervisorRequestDispatcher dispatcher,
        SupervisorRuntimeContext runtimeContext,
        TRequest request)
    {
        using var stream = new NonDisconnectingMemoryStream();
        await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default)
            .ConfigureAwait(false);
        var requestLength = stream.Length;
        stream.Position = 0;

        await dispatcher.HandleConnectionAsync(
                stream,
                runtimeContext,
                SupervisorConstants.InitialFrameReadTimeout,
                CancellationToken.None)
            .ConfigureAwait(false);

        stream.Position = requestLength;
        return await IpcFrameCodec.ReadModelAsync<IpcResponse>(
                stream,
                IpcJsonSerializerOptions.Default)
            .ConfigureAwait(false);
    }

    private static async Task<byte[]> CreateRequestFrameBytesAsync (IpcRequestEnvelope request)
    {
        using var stream = new MemoryStream();
        await IpcFrameCodec.WriteModelAsync(
                stream,
                request,
                IpcJsonSerializerOptions.Default)
            .ConfigureAwait(false);
        return stream.ToArray();
    }

    private sealed class FixedUtcTimeProvider : TimeProvider
    {
        public static FixedUtcTimeProvider Instance { get; } = new();

        public override DateTimeOffset GetUtcNow () => DefaultUtcNow;
    }
}
