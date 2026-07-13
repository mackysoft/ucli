namespace MackySoft.Ucli.Tests.Daemon;

using System.Net.Sockets;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Tests.Helpers.Process;
using static DaemonStartupReadinessProbeTestSupport;

public sealed class DaemonStartupReadinessProbeFailureClassificationTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenDaemonLogContainsCompilerErrorMarker_ReturnsInternalErrorImmediately ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(new SocketException((int)SocketError.ConnectionRefused));
        var logReader = new RecordingUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "Aborting batchmode due to failure:\nScripts have compiler errors.\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 128),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-readiness-compiler-marker"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.Contains("scripts have compiler errors", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Marker=Scripts have compiler errors.", error.Message, StringComparison.Ordinal);
        Assert.Equal(DaemonStartupBlockingReason.Compile, result.FailureClassification!.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDisposition.RetryAfterFix, result.FailureClassification.RetryDisposition);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenDaemonLogContainsCompilerErrorCode_ReturnsInternalErrorWithFirstErrorLine ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(new SocketException((int)SocketError.ConnectionRefused));
        var logReader = new RecordingUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                "Assets/Foo.cs(10,1): error CS0246: MissingType\n",
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 64),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-readiness-compiler-cs"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("FirstError=Assets/Foo.cs(10,1): error CS0246: MissingType", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenDaemonLogContainsPackageResolutionFailure_ReturnsInternalErrorImmediately ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(new SocketException((int)SocketError.ConnectionRefused));
        var logReader = new RecordingUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                """
                An error occurred while resolving packages:
                  Project has invalid dependencies:
                    com.unity.test-framework: Package [com.unity.test-framework@1.6.0] cannot be found
                """,
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 256),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-readiness-package-error"),
            TimeSpan.FromSeconds(5),
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, error.Code);
        Assert.Contains("package resolution failed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("FirstError=com.unity.test-framework: Package [com.unity.test-framework@1.6.0] cannot be found", error.Message, StringComparison.Ordinal);
        Assert.Equal(DaemonStartupBlockingReason.PackageResolution, result.FailureClassification!.StartupBlockingReason);
        Assert.Equal(DaemonStartupRetryDisposition.RetryAfterFix, result.FailureClassification.RetryDisposition);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task WaitUntilReady_WhenOnlyPreviousSessionHasPackageResolutionFailure_PreservesProcessExitFailure ()
    {
        var pingClient = new RecordingDaemonPingInfoClient(new SocketException((int)SocketError.ConnectionRefused));
        var logReader = new RecordingUnityLogReader
        {
            NextResult = UnityLogReadResult.Success(
                """
                COMMAND LINE ARGUMENTS:
                -projectPath
                /tmp/old
                An error occurred while resolving packages:
                  Project has invalid dependencies:
                    com.unity.modules.adaptiveperformance: Package [com.unity.modules.adaptiveperformance@1.0.0] cannot be found
                COMMAND LINE ARGUMENTS:
                -projectPath
                /tmp/new
                [Package Manager] Done resolving packages in 1.00 seconds
                """,
                truncated: false,
                path: "/tmp/unity.log",
                sizeBytes: 512),
        };
        var probe = CreateProbe(pingClient, logReader);

        var result = await probe.WaitUntilReadyAsync(
            ResolvedUnityProjectContextTestFactory.CreateDaemonLifecycleContext("fingerprint-readiness-ignore-previous-session-errors"),
            TimeSpan.FromSeconds(5),
            daemonProcessId: int.MaxValue,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsReady);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Null(error.Code);
        Assert.Contains("process exited before startup readiness was confirmed", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(result.FailureClassification);
        UnityLogReaderAssert.LogInspected(logReader);
    }
}
