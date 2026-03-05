using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Daemon;
using MackySoft.Ucli.Daemon.Command;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests.Daemon;

internal static class DaemonCommandServiceTestContext
{
    public static DaemonCommandExecutionContext CreateExecutionContext (int timeoutMilliseconds)
    {
        return new DaemonCommandExecutionContext(
            Context: new InitStatusContext(
                UnityProject: new ResolvedUnityProjectContext(
                    UnityProjectRoot: "/tmp/unity-project",
                    RepositoryRoot: "/tmp/repo-root",
                    ProjectFingerprint: "fingerprint",
                    PathSource: UnityProjectPathSource.CommandOption),
                Config: UcliConfig.CreateDefault(),
                ConfigSource: ConfigSource.Default),
            Timeout: TimeSpan.FromMilliseconds(timeoutMilliseconds));
    }

    public static DaemonSession CreateSession ()
    {
        return new DaemonSession(
            SchemaVersion: DaemonSession.CurrentSchemaVersion,
            SessionToken: "secret-token",
            ProjectFingerprint: "fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 05, 0, 0, 0, TimeSpan.Zero),
            RuntimeKind: DaemonSession.RuntimeKindBatchmode,
            OwnerKind: DaemonSession.OwnerKindCli,
            CanShutdownProcess: true,
            EndpointTransportKind: "namedPipe",
            EndpointAddress: "ucli-endpoint",
            ProcessId: 1234);
    }

    public static DaemonSessionOutput CreateSessionOutput ()
    {
        return new DaemonSessionOutput(
            ProjectFingerprint: "mapped-fingerprint",
            IssuedAtUtc: new DateTimeOffset(2026, 03, 05, 1, 2, 3, TimeSpan.Zero),
            RuntimeKind: "mapped-runtime",
            OwnerKind: "mapped-owner",
            CanShutdownProcess: false,
            EndpointTransportKind: "mapped-transport",
            EndpointAddress: "mapped-endpoint",
            ProcessId: 4321);
    }

    internal sealed class StubDaemonCommandExecutionContextResolver : IDaemonCommandExecutionContextResolver
    {
        private readonly DaemonCommandExecutionContextResolutionResult result;

        public StubDaemonCommandExecutionContextResolver (DaemonCommandExecutionContextResolutionResult result)
        {
            this.result = result;
        }

        public int CallCount { get; private set; }

        public string? LastProjectPath { get; private set; }

        public string? LastTimeoutOption { get; private set; }

        public UcliCommand LastTimeoutCommand { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonCommandExecutionContextResolutionResult> Resolve (
            UcliCommand timeoutCommand,
            string? projectPath,
            string? timeout,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastTimeoutCommand = timeoutCommand;
            LastProjectPath = projectPath;
            LastTimeoutOption = timeout;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(result);
        }
    }

    internal sealed class StubDaemonStartOperation : IDaemonStartOperation
    {
        public DaemonStartResult StartResult { get; set; } = DaemonStartResult.Started(CreateSession());

        public int StartCallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonStartResult> Start (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            StartCallCount++;
            LastUnityProject = unityProject;
            LastTimeout = timeout;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(StartResult);
        }
    }

    internal sealed class StubDaemonStopOperation : IDaemonStopOperation
    {
        public DaemonStopResult StopResult { get; set; } = DaemonStopResult.Stopped();

        public int StopCallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonStopResult> Stop (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            StopCallCount++;
            LastUnityProject = unityProject;
            LastTimeout = timeout;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(StopResult);
        }
    }

    internal sealed class StubDaemonStatusOperation : IDaemonStatusOperation
    {
        public DaemonStatusResult StatusResult { get; set; } = DaemonStatusResult.NotRunning();

        public int GetStatusCallCount { get; private set; }

        public ResolvedUnityProjectContext? LastUnityProject { get; private set; }

        public TimeSpan LastTimeout { get; private set; }

        public CancellationToken LastCancellationToken { get; private set; }

        public ValueTask<DaemonStatusResult> GetStatus (
            ResolvedUnityProjectContext unityProject,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            GetStatusCallCount++;
            LastUnityProject = unityProject;
            LastTimeout = timeout;
            LastCancellationToken = cancellationToken;
            return ValueTask.FromResult(StatusResult);
        }
    }

    internal sealed class StubDaemonSessionOutputMapper : IDaemonSessionOutputMapper
    {
        public DaemonSessionOutput Output { get; set; } = CreateSessionOutput();

        public DaemonSession? LastSession { get; private set; }

        public int CallCount { get; private set; }

        public DaemonSessionOutput ToOutput (DaemonSession session)
        {
            ArgumentNullException.ThrowIfNull(session);

            LastSession = session;
            CallCount++;
            return Output;
        }
    }
}