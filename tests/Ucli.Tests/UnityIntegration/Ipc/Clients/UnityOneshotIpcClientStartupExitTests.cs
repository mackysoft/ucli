using MackySoft.Tests;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using static MackySoft.Ucli.Tests.Ipc.UnityOneshotIpcClientTestSupport;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityOneshotIpcClientStartupExitTests
{
    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenUnityExitsAndStaleLockFileExists_ReturnsStartupExitFailureWithCleanupDiagnostic ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "exit-lock-file");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle(hasExited: true, exitCode: 1);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var client = new UnityOneshotIpcClient(
            launcher,
            new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Transport should not be called.")),
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(
                UnityProjectLockFileProbeResult.Locked(scope.GetPath("UnityProject/Temp/UnityLockfile"))));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonStartProcessExited, result.ErrorCode);
        Assert.Contains("exited before startup readiness", result.Message, StringComparison.Ordinal);
        Assert.Contains("Stale Unity project lock file was removed", result.Message, StringComparison.Ordinal);
        Assert.NotNull(result.FailureInfo!.StartupFailure);
        Assert.Equal("failed", result.FailureInfo.StartupFailure!.Startup!.StartupStatus);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Unknown), result.FailureInfo.StartupFailure.Startup.ProcessAction);
        UnityBatchmodeProcessHandleAssert.WasNotTerminated(processHandle);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenUnityExitsWithoutLockFile_ReturnsStartupExitFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "exit-unlocked");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle(hasExited: true, exitCode: 1);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var client = new UnityOneshotIpcClient(
            launcher,
            new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Transport should not be called.")),
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(
                UnityProjectLockFileProbeResult.Unlocked(scope.GetPath("UnityProject/Temp/UnityLockfile"))));

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonStartProcessExited, result.ErrorCode);
        Assert.Contains("exited before startup readiness", result.Message, StringComparison.Ordinal);
        Assert.NotNull(result.FailureInfo!.StartupFailure);
        Assert.Equal("failed", result.FailureInfo.StartupFailure!.Startup!.StartupStatus);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Unknown), result.FailureInfo.StartupFailure.Startup.ProcessAction);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenPostExitLockCleanupThrows_PreservesStartupExitFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "startup-exit-lock-cleanup-throws");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var processHandle = new StubUnityBatchmodeProcessHandle(hasExited: true, exitCode: 1);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var projectLockPreflightService = CreateProjectLockPreflightService();
        projectLockPreflightService.CleanupAsyncHandler = static (_, _) =>
            ValueTask.FromException<UnityProjectLockPreflightResult>(new IOException("lock cleanup failed"));
        var client = new UnityOneshotIpcClient(
            launcher,
            new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Transport should not be called.")),
            new StubProjectLifecycleLockProvider(),
            projectLockPreflightService);

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonStartProcessExited, result.ErrorCode);
        Assert.Contains("exited before startup readiness", result.Message, StringComparison.Ordinal);
        Assert.Contains("Post-exit Unity project lock cleanup failed", result.Message, StringComparison.Ordinal);
        Assert.Contains("lock cleanup failed", result.Message, StringComparison.Ordinal);
        Assert.NotNull(result.FailureInfo!.StartupFailure);
        Assert.Equal("failed", result.FailureInfo.StartupFailure!.Startup!.StartupStatus);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenUnityExitsWithCompileErrorLog_ReturnsClassifiedStartupFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "exit-compile-error");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var unityLogPath = scope.GetPath("UnityProject/Logs/Editor.log");
        var processHandle = new StubUnityBatchmodeProcessHandle(hasExited: true, exitCode: 1);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var logReader = new RecordingUnityLogReader(UnityLogReadResult.Success(
            """
            COMMAND LINE ARGUMENTS:
            Assets/Scripts/Broken.cs(10,5): error CS0246: The type or namespace name 'MissingType' could not be found
            Scripts have compiler errors.
            """,
            truncated: false,
            path: unityLogPath,
            sizeBytes: 192));
        var client = new UnityOneshotIpcClient(
            launcher,
            new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Transport should not be called.")),
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(
                UnityProjectLockFileProbeResult.Unlocked(scope.GetPath("UnityProject/Temp/UnityLockfile"))),
            logReader);

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, result.ErrorCode);
        Assert.NotNull(result.FailureInfo!.StartupFailure);
        var startupFailure = result.FailureInfo.StartupFailure!;
        Assert.Equal("blocked", startupFailure.Startup!.StartupStatus);
        Assert.Equal("compile", startupFailure.Startup.StartupBlockingReason);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Unknown), startupFailure.Startup.ProcessAction);
        Assert.Equal("unityScriptCompilationFailed", startupFailure.Diagnosis!.Reason);
        Assert.Equal("CS0246", startupFailure.Diagnosis.PrimaryDiagnostic!.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenUnityExitsWithPackageResolutionLog_ReturnsClassifiedStartupFailure ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "exit-package-resolution");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var unityLogPath = scope.GetPath("UnityProject/Logs/Editor.log");
        var processHandle = new StubUnityBatchmodeProcessHandle(hasExited: true, exitCode: 1);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var logReader = new RecordingUnityLogReader(UnityLogReadResult.Success(
            """
            COMMAND LINE ARGUMENTS:
            An error occurred while resolving packages:
            Project has invalid dependencies:
              com.example.missing: No package found for com.example.missing.
            """,
            truncated: false,
            path: unityLogPath,
            sizeBytes: 224));
        var client = new UnityOneshotIpcClient(
            launcher,
            new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Transport should not be called.")),
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(
                UnityProjectLockFileProbeResult.Unlocked(scope.GetPath("UnityProject/Temp/UnityLockfile"))),
            logReader);

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, result.ErrorCode);
        Assert.NotNull(result.FailureInfo!.StartupFailure);
        var startupFailure = result.FailureInfo.StartupFailure!;
        Assert.Equal("blocked", startupFailure.Startup!.StartupStatus);
        Assert.Equal("packageResolution", startupFailure.Startup.StartupBlockingReason);
        Assert.Equal(ContractLiteralCodec.ToValue(DaemonStartupProcessAction.Unknown), startupFailure.Startup.ProcessAction);
        Assert.Equal("unityPackageResolutionFailed", startupFailure.Diagnosis!.Reason);
        Assert.Equal("packageResolution", startupFailure.Diagnosis.PrimaryDiagnostic!.Kind);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task SendAsync_WhenClassifiedProcessExitHasStaleLockDiagnostic_PreservesCleanupMessage ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-oneshot-ipc-client", "exit-compile-error-stale-lock");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateForRepositoryRoot(scope.FullPath);
        var unityLogPath = scope.GetPath("UnityProject/Logs/Editor.log");
        var lockFilePath = scope.GetPath("UnityProject/Temp/UnityLockfile");
        var processHandle = new StubUnityBatchmodeProcessHandle(hasExited: true, exitCode: 1);
        var launcher = new RecordingUnityBatchmodeProcessLauncher(UnityBatchmodeProcessLaunchResult.Success(processHandle));
        var logReader = new RecordingUnityLogReader(UnityLogReadResult.Success(
            """
            COMMAND LINE ARGUMENTS:
            Assets/Scripts/Broken.cs(10,5): error CS0246: The type or namespace name 'MissingType' could not be found
            Scripts have compiler errors.
            """,
            truncated: false,
            path: unityLogPath,
            sizeBytes: 192));
        var client = new UnityOneshotIpcClient(
            launcher,
            new RecordingUnityIpcTransportClient(_ => throw new Xunit.Sdk.XunitException("Transport should not be called.")),
            new StubProjectLifecycleLockProvider(),
            CreateProjectLockPreflightService(
                UnityProjectLockFileProbeResult.Locked(lockFilePath)),
            logReader);

        var result = await client.SendAsync(
            unityProject,
            CreateDispatchRequest(),
            TimeSpan.FromSeconds(30),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(DaemonErrorCodes.DaemonStartupBlocked, result.ErrorCode);
        Assert.Contains("CS0246", result.Message, StringComparison.Ordinal);
        Assert.Contains("Stale Unity project lock file was removed", result.Message, StringComparison.Ordinal);
    }
}
