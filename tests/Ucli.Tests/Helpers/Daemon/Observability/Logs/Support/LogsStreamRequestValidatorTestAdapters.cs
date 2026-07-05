using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class LogsStreamRequestValidatorTestAdapters
{
    public static ILogsUnityRequestValidator CreateUnityZeroPollInterval (TimeSpan? idleTimeoutOverride = null)
    {
        return new ZeroPollIntervalLogsUnityRequestValidator(idleTimeoutOverride);
    }

    public static ILogsDaemonRequestValidator CreateDaemonZeroPollInterval (TimeSpan? idleTimeoutOverride = null)
    {
        return new ZeroPollIntervalLogsDaemonRequestValidator(idleTimeoutOverride);
    }

    private sealed class ZeroPollIntervalLogsUnityRequestValidator : ILogsUnityRequestValidator
    {
        private readonly LogsUnityRequestValidator inner = new();

        private readonly TimeSpan? idleTimeoutOverride;

        public ZeroPollIntervalLogsUnityRequestValidator (TimeSpan? idleTimeoutOverride)
        {
            this.idleTimeoutOverride = idleTimeoutOverride;
        }

        public bool TryValidate (
            LogsUnityServiceRequest request,
            out IpcUnityLogsReadRequest? query,
            out LogsStreamRuntimeOptions? streamOptions,
            out ExecutionError? error)
        {
            if (!inner.TryValidate(request, out query, out streamOptions, out error))
            {
                return false;
            }

            streamOptions = streamOptions! with
            {
                PollInterval = TimeSpan.Zero,
                IdleTimeout = idleTimeoutOverride ?? streamOptions.IdleTimeout,
            };
            return true;
        }
    }

    private sealed class ZeroPollIntervalLogsDaemonRequestValidator : ILogsDaemonRequestValidator
    {
        private readonly LogsDaemonRequestValidator inner = new();

        private readonly TimeSpan? idleTimeoutOverride;

        public ZeroPollIntervalLogsDaemonRequestValidator (TimeSpan? idleTimeoutOverride)
        {
            this.idleTimeoutOverride = idleTimeoutOverride;
        }

        public bool TryValidate (
            LogsDaemonServiceRequest request,
            out IpcDaemonLogsReadRequest? query,
            out LogsStreamRuntimeOptions? streamOptions,
            out ExecutionError? error)
        {
            if (!inner.TryValidate(request, out query, out streamOptions, out error))
            {
                return false;
            }

            streamOptions = streamOptions! with
            {
                PollInterval = TimeSpan.Zero,
                IdleTimeout = idleTimeoutOverride ?? streamOptions.IdleTimeout,
            };
            return true;
        }
    }
}
