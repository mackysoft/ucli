using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
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
    public void BuildArgumentTokens_WhenWindowsPathsContainSpaces_PreservesRawPathTokens ()
    {
        var projectPath = @"C:\Users\Foo Bar\Project";
        var unityLogPath = @"C:\Users\Foo Bar\Project\.ucli\unity.log";
        var bootstrapEnvelope = CreateBootstrapEnvelope();
        var bootstrapArguments = new IpcOneshotBootstrapArguments(bootstrapEnvelope.BootstrapId);

        var arguments = UnityBatchmodeProcessLauncher.BuildArgumentTokens(projectPath, unityLogPath, bootstrapArguments);

        Assert.Equal("-projectPath", arguments[2]);
        Assert.Equal(projectPath, arguments[3]);
        Assert.Equal("-logFile", arguments[4]);
        Assert.Equal(unityLogPath, arguments[5]);
        Assert.Contains("-executeMethod", arguments);
        Assert.Contains("MackySoft.Ucli.Unity.Editor.BuildExecuteMethodBridge.Run", arguments);
        Assert.DoesNotContain(@"C:\\Users\\Foo Bar\\Project", arguments, StringComparer.Ordinal);
        Assert.DoesNotContain(@"C:\\Users\\Foo Bar\\Project\\.ucli\\unity.log", arguments, StringComparer.Ordinal);
        Assert.DoesNotContain(bootstrapEnvelope.SessionToken.GetEncodedValue(), arguments, StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArgumentTokens_WithActiveBuildProfile_AddsUnityActiveBuildProfileArguments ()
    {
        var bootstrapArguments = new IpcOneshotBootstrapArguments(Guid.NewGuid());

        var arguments = UnityBatchmodeProcessLauncher.BuildArgumentTokens(
            "/tmp/unity-project",
            "/tmp/unity.log",
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
            scope.GetPath("unity.log"),
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
        var lockFilePath = Path.Combine(unityProject.UnityProjectRoot, "Temp", "UnityLockfile");
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
            scope.GetPath("unity.log"),
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
                Path.Combine(unityProject.UnityProjectRoot, "Temp", "UnityLockfile"),
                "lock owner could not be inspected")));

        var result = await launcher.LaunchOneshotAsync(
            unityProject,
            CreateBootstrapEnvelope(unityProject.RepositoryRoot),
            Path.Combine(unityProject.RepositoryRoot, "unity.log"),
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
        var lockFilePath = Path.Combine(unityProject.UnityProjectRoot, "Temp", "UnityLockfile");
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
            scope.GetPath("unity.log"),
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
                        Path.Combine(unityProject.RepositoryRoot, "ucli-plugin.json"),
                        UnityUcliPluginMarkerContract.ExpectedProtocolVersion));
                },
            },
            lockPreflightService);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            _ = await launcher.LaunchOneshotAsync(
                unityProject,
                CreateBootstrapEnvelope(unityProject.RepositoryRoot),
                scope.GetPath("unity.log"),
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
        var envelope = CreateBootstrapEnvelope(scope.FullPath);
        var launcher = new UnityBatchmodeProcessLauncher(
            new UnexpectedUnityVersionResolver("Preflight failure must stop before Unity version resolution."),
            new StubUnityEditorPathResolver(),
            new RecordingUnityUcliPluginLocator(),
            new RecordingUnityProjectLockPreflightService(UnityProjectLockPreflightResult.Ambiguous(
                scope.GetPath("unity-project/Temp/UnityLockfile"),
                "lock owner could not be inspected")));

        var result = await launcher.LaunchOneshotAsync(
            unityProject,
            envelope,
            scope.GetPath("unity.log"),
            UnityBatchmodeLaunchOptions.Default,
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.False(File.Exists(UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            scope.FullPath,
            envelope.ProjectFingerprint,
            envelope.BootstrapId)));
    }

    [Fact]
    [Trait("Size", "Medium")]
    public async Task ProcessHandleDispose_AfterSuccessfulOwnershipTransfer_DeletesBootstrapEnvelope ()
    {
        using var scope = TestDirectories.CreateTempScope("unity-batchmode-process-launcher", "bootstrap-success-cleanup");
        var envelope = CreateBootstrapEnvelope(scope.FullPath);
        OneshotBootstrapEnvelopeStore.Create(scope.FullPath, envelope);
        var innerHandle = new StubUnityBatchmodeProcessHandle();
        var handle = new OneshotBootstrapOwnedProcessHandle(innerHandle, scope.FullPath, envelope);

        await handle.DisposeAsync();

        Assert.Equal(1, innerHandle.DisposeCount);
        Assert.False(File.Exists(UcliStoragePathResolver.ResolveOneshotBootstrapPath(
            scope.FullPath,
            envelope.ProjectFingerprint,
            envelope.BootstrapId)));
    }

    private static IpcOneshotBootstrapEnvelope CreateBootstrapEnvelope ()
    {
        return CreateBootstrapEnvelope(ProjectPathTestValues.RepositoryRoot);
    }

    private static IpcOneshotBootstrapEnvelope CreateBootstrapEnvelope (string storageRoot)
    {
        var createdAtUtc = DateTimeOffset.UtcNow;
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        return new IpcOneshotBootstrapEnvelope(
            BootstrapId: Guid.NewGuid(),
            ParentProcessId: process.Id,
            ParentProcessStartedAtUtc: new DateTimeOffset(process.StartTime.ToUniversalTime()),
            ProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
            SessionToken: IpcSessionTokenTestFactory.Create("session-token"),
            CreatedAtUtc: createdAtUtc,
            ExitDeadlineUtc: createdAtUtc.AddMinutes(5),
            Endpoint: UcliIpcEndpointResolver.ResolveDaemonEndpoint(
                storageRoot,
                ProjectFingerprintTestFactory.Create("project-fingerprint")));
    }

}
