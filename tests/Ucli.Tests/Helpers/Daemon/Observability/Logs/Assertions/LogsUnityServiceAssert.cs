using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Tests.Helpers.Daemon;

internal static class LogsUnityServiceAssert
{
    public static void InvalidStackTraceRejectedBeforeContextResolution (
        LogsReadServiceResult result,
        RecordingDaemonCommandExecutionContextResolver resolver)
    {
        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Contains(
            "stackTrace must be one of: none, error, all.",
            error.Message,
            StringComparison.Ordinal);
        Assert.Empty(resolver.Invocations);
    }
}
