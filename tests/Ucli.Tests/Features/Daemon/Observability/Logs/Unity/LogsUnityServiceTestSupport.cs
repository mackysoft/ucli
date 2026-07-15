using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Tests.Helpers.Daemon;

namespace MackySoft.Ucli.Tests.Logs;

internal static class LogsUnityServiceTestSupport
{
    public static RecordingDaemonCommandExecutionContextResolver CreateResolver (int timeoutMilliseconds = 3000)
    {
        return new RecordingDaemonCommandExecutionContextResolver(
            DaemonCommandExecutionContextResolutionResult.Success(
                DaemonCommandExecutionContextTestFactory.Create(
                    timeoutMilliseconds: timeoutMilliseconds,
                    unityVersion: ProjectIdentityDefaults.UnknownUnityVersion)));
    }

    public static IpcUnityLogEvent CreateEvent (
        string cursor,
        string message,
        DateTimeOffset timestamp)
    {
        return new IpcUnityLogEvent(
            Timestamp: timestamp,
            Level: IpcLogLevel.Info,
            Source: IpcUnityLogSource.Runtime,
            Message: message,
            StackTrace: null,
            Cursor: new IpcLogCursor(cursor));
    }

    public static IpcUnityLogsReadResponse CreatePayload (
        IpcUnityLogEvent[] events,
        string nextCursor)
    {
        return new IpcUnityLogsReadResponse(events, new IpcLogCursor(nextCursor));
    }

    public static LogsUnityService CreateService (
        IDaemonCommandExecutionContextResolver resolver,
        IUnityLogsClient unityLogsClient,
        ILogsUnityRequestValidator? requestValidator = null)
    {
        return new LogsUnityService(
            new LogsStreamPollingExecutor(resolver, TimeProvider.System),
            unityLogsClient,
            requestValidator ?? new LogsUnityRequestValidator());
    }

    public static LogsUnityService CreateZeroPollIntervalService (
        IDaemonCommandExecutionContextResolver resolver,
        IUnityLogsClient unityLogsClient)
    {
        return CreateService(
            resolver,
            unityLogsClient,
            LogsStreamRequestValidatorTestAdapters.CreateUnityZeroPollInterval());
    }

    public static LogsUnityService CreateImmediateIdleStreamService (
        IDaemonCommandExecutionContextResolver resolver,
        IUnityLogsClient unityLogsClient)
    {
        return CreateService(
            resolver,
            unityLogsClient,
            LogsStreamRequestValidatorTestAdapters.CreateUnityZeroPollInterval(TimeSpan.Zero));
    }
}
