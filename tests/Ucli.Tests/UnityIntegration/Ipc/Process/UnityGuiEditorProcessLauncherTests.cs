using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Project.Plugin;

namespace MackySoft.Ucli.Tests.UnityIntegration.Ipc.Process;

public sealed class UnityGuiEditorProcessLauncherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArgumentTokens_IncludesGuiBootstrapArgumentsAndOmitsBatchmodeArguments ()
    {
        var tokens = UnityGuiEditorProcessLauncher.BuildArgumentTokens(
            "/repo/UnityProject",
            "/repo/.ucli/logs/unity.log",
            new IpcGuiBootstrapArguments(
                OwnerProcessId: 123,
                CanShutdownProcess: true));

        Assert.DoesNotContain("-batchmode", tokens);
        Assert.DoesNotContain("-nographics", tokens);
        Assert.Equal(
            [
                "-projectPath",
                "/repo/UnityProject",
                "-logFile",
                "/repo/.ucli/logs/unity.log",
                IpcGuiBootstrapArgumentNames.Target,
                IpcGuiBootstrapTargetValues.Daemon,
                IpcGuiBootstrapArgumentNames.OwnerProcessId,
                "123",
                IpcGuiBootstrapArgumentNames.CanShutdownProcess,
                "true",
            ],
            tokens);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenUnityLockFileExists_ReturnsAlreadyOpenWithoutResolvingUnityVersion ()
    {
        var versionResolver = new StubUnityVersionResolver();
        var launcher = new UnityGuiEditorProcessLauncher(
            versionResolver,
            new StubUnityEditorPathResolver(),
            new StubUnityUcliPluginLocator(),
            new StubUnityProjectLockPreflightService(UnityProjectLockPreflightResult.ActiveLock(
                "/tmp/unity-project/Temp/UnityLockfile",
                "Unity project is already open.")));

        var result = await launcher.LaunchAsync(
            CreateContext(),
            "/tmp/unity.log",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectAlreadyOpen, error.Code);
        Assert.Equal(0, versionResolver.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenUnityLockFileAppearsBeforeProcessStart_ReturnsAlreadyOpen ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-gui-editor-process-launcher", "lock-file-race");
        var versionResolver = new StubUnityVersionResolver();
        var lockPreflightService = new StubUnityProjectLockPreflightService(
            UnityProjectLockPreflightResult.Unlocked("/tmp/unity-project/Temp/UnityLockfile"),
            UnityProjectLockPreflightResult.ActiveLock(
                "/tmp/unity-project/Temp/UnityLockfile",
                "Unity project is already open."));
        var launcher = new UnityGuiEditorProcessLauncher(
            versionResolver,
            new StubUnityEditorPathResolver("/path/that/must/not/exist/ucli-unity"),
            new StubUnityUcliPluginLocator(),
            lockPreflightService);
        var unityLogPath = scope.GetPath("unity.log");
        File.WriteAllText(unityLogPath, "old compiler error");

        var result = await launcher.LaunchAsync(
            CreateContext(),
            unityLogPath,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(2, lockPreflightService.CallCount);
        Assert.Equal(1, versionResolver.CallCount);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectAlreadyOpen, error.Code);
        Assert.False(File.Exists(unityLogPath));
    }

    private static ResolvedUnityProjectContext CreateContext ()
    {
        return new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/unity-project",
            RepositoryRoot: "/tmp/repository-root",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
    }

    private sealed class StubUnityVersionResolver : IUnityVersionResolver
    {
        public int CallCount { get; private set; }

        public UnityVersionResolutionResult Resolve (
            string unityProjectRoot,
            string? preferredUnityVersion)
        {
            CallCount++;
            return UnityVersionResolutionResult.Success("2023.2.22f1");
        }
    }

    private sealed class StubUnityEditorPathResolver : IUnityEditorPathResolver
    {
        private readonly string unityEditorPath;

        public StubUnityEditorPathResolver (string unityEditorPath = "/Applications/Unity.app/Contents/MacOS/Unity")
        {
            this.unityEditorPath = unityEditorPath;
        }

        public UnityEditorPathResolutionResult Resolve (
            string unityVersion,
            string? preferredUnityEditorPath)
        {
            return UnityEditorPathResolutionResult.Success(unityEditorPath);
        }
    }

    private sealed class StubUnityUcliPluginLocator : IUnityUcliPluginLocator
    {
        public ValueTask<UnityUcliPluginLocateResult> LocateAsync (
            string unityProjectRoot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(UnityUcliPluginLocateResult.Found(
                "/tmp/ucli-plugin.json",
                UnityUcliPluginLocator.ExpectedProtocolVersion));
        }
    }

    private sealed class StubUnityProjectLockPreflightService : IUnityProjectLockPreflightService
    {
        private readonly IReadOnlyList<UnityProjectLockPreflightResult> results;

        private int nextResultIndex;

        public StubUnityProjectLockPreflightService (params UnityProjectLockPreflightResult[] results)
        {
            this.results = results is { Length: > 0 }
                ? results
                : [UnityProjectLockPreflightResult.Unlocked("/tmp/unity-project/Temp/UnityLockfile")];
        }

        public int CallCount { get; private set; }

        public ValueTask<UnityProjectLockPreflightResult> PrepareForUnityProcessStartAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resultIndex = Math.Min(nextResultIndex, results.Count - 1);
            nextResultIndex++;
            CallCount++;
            return ValueTask.FromResult(results[resultIndex]);
        }

        public ValueTask<UnityProjectLockPreflightResult> CleanupStaleLockAfterUnityProcessExitAsync (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
