using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Unity;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

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
                "daemon",
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
        var unityProject = ResolvedUnityProjectContextTestFactory.Create();
        var lockFilePath = Path.Combine(unityProject.UnityProjectRoot, "Temp", "UnityLockfile");
        var launcher = new UnityGuiEditorProcessLauncher(
            new UnexpectedUnityVersionResolver("Active Unity project lock must stop before Unity version resolution."),
            new StubUnityEditorPathResolver(),
            new RecordingUnityUcliPluginLocator(),
            new RecordingUnityProjectLockPreflightService(UnityProjectLockPreflightResult.ActiveLock(
                lockFilePath,
                "Unity project is already open.")));

        var result = await launcher.LaunchAsync(
            unityProject,
            Path.Combine(unityProject.RepositoryRoot, "unity.log"),
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectAlreadyOpen, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Launch_WhenUnityLockFileAppearsBeforeProcessStart_ReturnsAlreadyOpen ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-gui-editor-process-launcher", "lock-file-race");
        var versionResolver = new RecordingUnityVersionResolver();
        var unityProject = ResolvedUnityProjectContextTestFactory.Create();
        var lockFilePath = Path.Combine(unityProject.UnityProjectRoot, "Temp", "UnityLockfile");
        var lockPreflightService = new RecordingUnityProjectLockPreflightService(
            UnityProjectLockPreflightResult.Unlocked(lockFilePath),
            UnityProjectLockPreflightResult.ActiveLock(
                lockFilePath,
                "Unity project is already open."));
        var launcher = new UnityGuiEditorProcessLauncher(
            versionResolver,
            new StubUnityEditorPathResolver("/path/that/must/not/exist/ucli-unity"),
            new RecordingUnityUcliPluginLocator(),
            lockPreflightService);
        var unityLogPath = scope.GetPath("unity.log");
        File.WriteAllText(unityLogPath, "old compiler error");

        var result = await launcher.LaunchAsync(
            unityProject,
            unityLogPath,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        lockPreflightService.AssertStartPreflightRetriedFor(unityProject, CancellationToken.None);
        UnityVersionResolverAssert.ResolvedOnceFor(versionResolver, unityProject.UnityProjectRoot);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectAlreadyOpen, error.Code);
        Assert.False(File.Exists(unityLogPath));
    }

}
