using MackySoft.FileSystem;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Infrastructure.Execution;
using MackySoft.Ucli.Infrastructure.Ipc;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
using MackySoft.Ucli.Tests.Helpers.Process;
using MackySoft.Ucli.Tests.Helpers.Unity;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityBatchmodeProcessLauncherTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void BuildArgumentTokens_WhenPathsContainSpaces_PreservesPlatformPathTokens ()
    {
        var projectPath = AbsolutePath.Parse(
            OperatingSystem.IsWindows()
                ? @"C:\Users\Foo Bar\Project"
                : "/tmp/Foo Bar/Project");
        var unityLogPath = AbsolutePath.Parse(
            OperatingSystem.IsWindows()
                ? @"C:\Users\Foo Bar\Project\.ucli\unity.log"
                : "/tmp/Foo Bar/Project/.ucli/unity.log");
        var bootstrapEnvelope = CreateBootstrapEnvelope();
        var bootstrapArguments = new IpcOneshotBootstrapArguments(bootstrapEnvelope.BootstrapId);

        var arguments = UnityBatchmodeProcessLauncher.BuildArgumentTokens(projectPath, unityLogPath, bootstrapArguments);

        Assert.Equal("-projectPath", arguments[2]);
        Assert.Equal(projectPath.Value, arguments[3]);
        Assert.Equal("-logFile", arguments[4]);
        Assert.Equal(unityLogPath.Value, arguments[5]);
        Assert.Contains("-executeMethod", arguments);
        Assert.Contains("MackySoft.Ucli.Unity.Editor.BuildExecuteMethodBridge.Run", arguments);
        if (OperatingSystem.IsWindows())
        {
            Assert.DoesNotContain(projectPath.Value.Replace(@"\", @"\\"), arguments, StringComparer.Ordinal);
            Assert.DoesNotContain(unityLogPath.Value.Replace(@"\", @"\\"), arguments, StringComparer.Ordinal);
        }

        Assert.DoesNotContain(bootstrapEnvelope.SessionToken.GetEncodedValue(), arguments, StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArgumentTokens_WithActiveBuildProfile_AddsUnityActiveBuildProfileArguments ()
    {
        var bootstrapArguments = new IpcOneshotBootstrapArguments(Guid.NewGuid());
        var projectPath = AbsolutePath.Parse(Path.Combine(Path.GetTempPath(), "unity-project"));
        var unityLogPath = AbsolutePath.Parse(Path.Combine(Path.GetTempPath(), "unity.log"));

        var arguments = UnityBatchmodeProcessLauncher.BuildArgumentTokens(
            projectPath,
            unityLogPath,
            bootstrapArguments,
            new UnityBatchmodeLaunchOptions(
                new UnityBuildProfileAssetPath("Assets/BuildProfiles/LinuxPlayer.asset")));

        var argumentArray = arguments.ToArray();
        var activeBuildProfileIndex = Array.IndexOf(argumentArray, "-activeBuildProfile");
        Assert.True(activeBuildProfileIndex >= 0);
        Assert.Equal("Assets/BuildProfiles/LinuxPlayer.asset", arguments[activeBuildProfileIndex + 1]);
        Assert.True(activeBuildProfileIndex < Array.IndexOf(argumentArray, "-executeMethod"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenUnityPluginMarkerIsMissing_ReturnsInvalidArgumentWithoutResolvingUnityVersion ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-batchmode-process-launcher", "plugin-marker-missing");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithPaths(
            unityProjectRoot: scope.GetPath("UnityProject"),
            repositoryRoot: scope.FullPath);
        var launcher = new UnityBatchmodeProcessLauncher(
            new UnexpectedUnityVersionResolver("Plugin validation failure must stop before Unity version resolution."),
            new StubUnityEditorPathResolver(),
            new RecordingUnityUcliPluginLocator
            {
                Result = UnityUcliPluginLocateResult.NotFound(ExecutionError.InvalidArgument(
                    "Unity project does not contain the uCLI Unity plugin.")),
            },
            new RecordingUnityProjectLockPreflightService());

        var result = await launcher.LaunchOneshotAsync(
            unityProject,
            CreateBootstrapEnvelope(unityProject.RepositoryRoot),
            AbsolutePath.Parse(scope.GetPath("unity.log")),
            UnityBatchmodeLaunchOptions.Default,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenUnityLockFileExists_ReturnsAlreadyOpenWithoutResolvingUnityVersion ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-batchmode-process-launcher", "active-lock");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithPaths(
            unityProjectRoot: scope.GetPath("UnityProject"),
            repositoryRoot: scope.FullPath);
        var lockFilePath = AbsolutePath.Resolve(unityProject.UnityProjectRoot, "Temp/UnityLockfile");
        var launcher = new UnityBatchmodeProcessLauncher(
            new UnexpectedUnityVersionResolver("Active Unity project lock must stop before Unity version resolution."),
            new StubUnityEditorPathResolver(),
            new RecordingUnityUcliPluginLocator(),
            new RecordingUnityProjectLockPreflightService(UnityProjectLockPreflightResult.ActiveLock(
                lockFilePath,
                UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProject.UnityProjectRoot, lockFilePath))));

        var result = await launcher.LaunchOneshotAsync(
            unityProject,
            CreateBootstrapEnvelope(unityProject.RepositoryRoot),
            AbsolutePath.Parse(scope.GetPath("unity.log")),
            UnityBatchmodeLaunchOptions.Default,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectAlreadyOpen, error.Code);
        Assert.Contains("UnityLockfile", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenProjectLockOwnershipIsAmbiguous_ReturnsLockAmbiguousWithoutResolvingUnityVersion ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-batchmode-process-launcher", "ambiguous-lock");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithPaths(
            unityProjectRoot: scope.GetPath("UnityProject"),
            repositoryRoot: scope.FullPath);
        var launcher = new UnityBatchmodeProcessLauncher(
            new UnexpectedUnityVersionResolver("Ambiguous Unity project lock must stop before Unity version resolution."),
            new StubUnityEditorPathResolver(),
            new RecordingUnityUcliPluginLocator(),
            new RecordingUnityProjectLockPreflightService(UnityProjectLockPreflightResult.Ambiguous(
                AbsolutePath.Resolve(unityProject.UnityProjectRoot, "Temp/UnityLockfile"),
                "lock owner could not be inspected")));

        var result = await launcher.LaunchOneshotAsync(
            unityProject,
            CreateBootstrapEnvelope(unityProject.RepositoryRoot),
            AbsolutePath.Resolve(unityProject.RepositoryRoot, "unity.log"),
            UnityBatchmodeLaunchOptions.Default,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectLockAmbiguous, error.Code);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Launch_WhenUnityLockFileAppearsBeforeProcessStart_ReturnsAlreadyOpen ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-batchmode-process-launcher", "lock-file-race");
        var versionResolver = new RecordingUnityVersionResolver();
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithPaths(
            unityProjectRoot: scope.GetPath("UnityProject"),
            repositoryRoot: scope.FullPath);
        var lockFilePath = AbsolutePath.Resolve(unityProject.UnityProjectRoot, "Temp/UnityLockfile");
        var lockPreflightService = new RecordingUnityProjectLockPreflightService(
            UnityProjectLockPreflightResult.Unlocked(lockFilePath),
            UnityProjectLockPreflightResult.ActiveLock(
                lockFilePath,
                UnityProjectLockFailureMessage.CreateAlreadyOpen(unityProject.UnityProjectRoot, lockFilePath)));
        var launcher = new UnityBatchmodeProcessLauncher(
            versionResolver,
            new StubUnityEditorPathResolver("/path/that/must/not/exist/ucli-unity"),
            new RecordingUnityUcliPluginLocator(),
            lockPreflightService);

        var result = await launcher.LaunchOneshotAsync(
            unityProject,
            CreateBootstrapEnvelope(unityProject.RepositoryRoot),
            AbsolutePath.Parse(scope.GetPath("unity.log")),
            UnityBatchmodeLaunchOptions.Default,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        lockPreflightService.AssertStartPreflightRetriedFor(unityProject, CancellationToken.None);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Equal(UnityProcessErrorCodes.UnityProjectAlreadyOpen, error.Code);
        Assert.Contains("UnityLockfile", error.Message, StringComparison.Ordinal);
        UnityVersionResolverAssert.ResolvedOnceFor(
            versionResolver,
            unityProject.UnityProjectRoot);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task Launch_WhenCanceledBeforeProcessStart_ThrowsWithoutSecondLockProbe ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-batchmode-process-launcher", "canceled-before-start");
        using var cancellationTokenSource = new CancellationTokenSource();
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithPaths(
            unityProjectRoot: scope.GetPath("UnityProject"),
            repositoryRoot: scope.FullPath);
        var lockPreflightService = new RecordingUnityProjectLockPreflightService();
        var launcher = new UnityBatchmodeProcessLauncher(
            new RecordingUnityVersionResolver(),
            new StubUnityEditorPathResolver("/path/that/must/not/exist/ucli-unity"),
            new RecordingUnityUcliPluginLocator
            {
                Handler = _ =>
                {
                    cancellationTokenSource.Cancel();
                    return ValueTask.FromResult(UnityUcliPluginLocateResult.Found(
                        AbsolutePath.Resolve(unityProject.RepositoryRoot, "ucli-plugin.json"),
                        UnityUcliPluginMarkerContract.ExpectedProtocolVersion));
                },
            },
            lockPreflightService);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            _ = await launcher.LaunchOneshotAsync(
                unityProject,
                CreateBootstrapEnvelope(unityProject.RepositoryRoot),
                AbsolutePath.Parse(scope.GetPath("unity.log")),
                UnityBatchmodeLaunchOptions.Default,
                cancellationTokenSource.Token);
        });

        lockPreflightService.AssertOnlyInitialStartPreflightFor(unityProject, cancellationTokenSource.Token);
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task LaunchOneshot_WhenPreflightFails_DeletesBootstrapEnvelope ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-batchmode-process-launcher", "bootstrap-launch-failure");
        var unityProject = ResolvedUnityProjectContextTestFactory.CreateWithPaths(
            unityProjectRoot: scope.GetPath("unity-project"),
            repositoryRoot: scope.FullPath);
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var envelope = CreateBootstrapEnvelope(storageRoot);
        var launcher = new UnityBatchmodeProcessLauncher(
            new UnexpectedUnityVersionResolver("Preflight failure must stop before Unity version resolution."),
            new StubUnityEditorPathResolver(),
            new RecordingUnityUcliPluginLocator(),
            new RecordingUnityProjectLockPreflightService(UnityProjectLockPreflightResult.Ambiguous(
                AbsolutePath.Parse(scope.GetPath("unity-project/Temp/UnityLockfile")),
                "lock owner could not be inspected")));

        var result = await launcher.LaunchOneshotAsync(
            unityProject,
            envelope,
            AbsolutePath.Parse(scope.GetPath("unity.log")),
            UnityBatchmodeLaunchOptions.Default,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            storageRoot,
            envelope.ProjectFingerprint,
            envelope.BootstrapId).Value));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ProcessHandleDispose_AfterSuccessfulOwnershipTransfer_DeletesBootstrapEnvelope ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-batchmode-process-launcher", "bootstrap-success-cleanup");
        var storageRoot = AbsolutePath.Parse(scope.FullPath);
        var envelope = CreateBootstrapEnvelope(storageRoot);
        OneshotBootstrapEnvelopeStore.Create(storageRoot, envelope);
        var innerHandle = new StubUnityBatchmodeProcessHandle();
        var handle = new OneshotBootstrapOwnedProcessHandle(innerHandle, storageRoot, envelope);

        await handle.DisposeAsync();

        Assert.Equal(1, innerHandle.DisposeCount);
        Assert.False(File.Exists(UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            storageRoot,
            envelope.ProjectFingerprint,
            envelope.BootstrapId).Value));
    }

    private static IpcOneshotBootstrapEnvelope CreateBootstrapEnvelope ()
    {
        return CreateBootstrapEnvelope(AbsolutePath.Parse(ProjectPathTestValues.RepositoryRoot));
    }

    private static IpcOneshotBootstrapEnvelope CreateBootstrapEnvelope (AbsolutePath storageRoot)
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        return new IpcOneshotBootstrapEnvelope(
            BootstrapId: Guid.NewGuid(),
            ParentProcess: ProcessLivenessProbe.CaptureCurrentProcess(),
            ProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
            SessionToken: IpcSessionTokenTestFactory.Create("session-token"),
            CreatedAtUtc: createdAtUtc,
            ExitDeadlineUtc: createdAtUtc.AddMinutes(5),
            Endpoint: UcliIpcEndpointResolver.ResolveDaemonEndpoint(
                storageRoot,
                ProjectFingerprintTestFactory.Create("project-fingerprint")).Contract);
    }

}
