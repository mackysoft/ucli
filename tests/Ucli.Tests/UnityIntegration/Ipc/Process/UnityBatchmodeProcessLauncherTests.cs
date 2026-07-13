using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Shared.Unity.ProjectLock;
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
        var bootstrapArguments = new IpcOneshotBootstrapArguments(
            ParentProcessId: 1234,
            ProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
            SessionToken: "session-token",
            ExitDeadlineUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: @"\\.\pipe\ucli-oneshot");

        var arguments = UnityBatchmodeProcessLauncher.BuildArgumentTokens(projectPath, unityLogPath, bootstrapArguments);

        Assert.Equal("-projectPath", arguments[2]);
        Assert.Equal(projectPath, arguments[3]);
        Assert.Equal("-logFile", arguments[4]);
        Assert.Equal(unityLogPath, arguments[5]);
        Assert.Contains("-executeMethod", arguments);
        Assert.Contains("MackySoft.Ucli.Unity.Editor.BuildExecuteMethodBridge.Run", arguments);
        Assert.DoesNotContain(@"C:\\Users\\Foo Bar\\Project", arguments, StringComparer.Ordinal);
        Assert.DoesNotContain(@"C:\\Users\\Foo Bar\\Project\\.ucli\\unity.log", arguments, StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void BuildArgumentTokens_WithActiveBuildProfile_AddsUnityActiveBuildProfileArguments ()
    {
        var bootstrapArguments = new IpcOneshotBootstrapArguments(
            ParentProcessId: 1234,
            ProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
            SessionToken: "session-token",
            ExitDeadlineUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
            EndpointTransportKind: "unixDomainSocket",
            EndpointAddress: "/tmp/ucli.sock");

        var arguments = UnityBatchmodeProcessLauncher.BuildArgumentTokens(
            "/tmp/unity-project",
            "/tmp/unity.log",
            bootstrapArguments,
            new UnityBatchmodeLaunchOptions("Assets/BuildProfiles/LinuxPlayer.asset"));

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
        var launcher = new UnityBatchmodeProcessLauncher(
            new UnexpectedUnityVersionResolver("Plugin validation failure must stop before Unity version resolution."),
            new StubUnityEditorPathResolver(),
            new RecordingUnityUcliPluginLocator
            {
                Result = UnityUcliPluginLocateResult.NotFound(ExecutionError.InvalidArgument(
                    "Unity project does not contain the uCLI Unity plugin.")),
            },
            new RecordingUnityProjectLockPreflightService());

        var result = await launcher.LaunchAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: "/tmp/repository-root"),
            new IpcOneshotBootstrapArguments(
                ParentProcessId: 1234,
                ProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
                SessionToken: "session-token",
                ExitDeadlineUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
                EndpointTransportKind: "unixDomainSocket",
                EndpointAddress: "/tmp/ucli.sock"),
            "/tmp/unity.log",
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
        var lockFilePath = "/tmp/unity-project/Temp/UnityLockfile";
        var launcher = new UnityBatchmodeProcessLauncher(
            new UnexpectedUnityVersionResolver("Active Unity project lock must stop before Unity version resolution."),
            new StubUnityEditorPathResolver(),
            new RecordingUnityUcliPluginLocator(),
            new RecordingUnityProjectLockPreflightService(UnityProjectLockPreflightResult.ActiveLock(
                lockFilePath,
                UnityProjectLockFailureMessage.CreateAlreadyOpen("/tmp/unity-project", lockFilePath))));

        var result = await launcher.LaunchAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: "/tmp/repository-root"),
            new IpcOneshotBootstrapArguments(
                ParentProcessId: 1234,
                ProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
                SessionToken: "session-token",
                ExitDeadlineUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
                EndpointTransportKind: "unixDomainSocket",
                EndpointAddress: "/tmp/ucli.sock"),
            "/tmp/unity.log",
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
        var launcher = new UnityBatchmodeProcessLauncher(
            new UnexpectedUnityVersionResolver("Ambiguous Unity project lock must stop before Unity version resolution."),
            new StubUnityEditorPathResolver(),
            new RecordingUnityUcliPluginLocator(),
            new RecordingUnityProjectLockPreflightService(UnityProjectLockPreflightResult.Ambiguous(
                "/tmp/unity-project/Temp/UnityLockfile",
                "lock owner could not be inspected")));

        var result = await launcher.LaunchAsync(
            ResolvedUnityProjectContextTestFactory.Create(
                unityProjectRoot: "/tmp/unity-project",
                repositoryRoot: "/tmp/repository-root"),
            new IpcOneshotBootstrapArguments(
                ParentProcessId: 1234,
                ProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
                SessionToken: "session-token",
                ExitDeadlineUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
                EndpointTransportKind: "unixDomainSocket",
                EndpointAddress: "/tmp/ucli.sock"),
            "/tmp/unity.log",
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
        var lockFilePath = "/tmp/unity-project/Temp/UnityLockfile";
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: "/tmp/repository-root");
        var lockPreflightService = new RecordingUnityProjectLockPreflightService(
            UnityProjectLockPreflightResult.Unlocked(lockFilePath),
            UnityProjectLockPreflightResult.ActiveLock(
                lockFilePath,
                UnityProjectLockFailureMessage.CreateAlreadyOpen("/tmp/unity-project", lockFilePath)));
        var launcher = new UnityBatchmodeProcessLauncher(
            versionResolver,
            new StubUnityEditorPathResolver("/path/that/must/not/exist/ucli-unity"),
            new RecordingUnityUcliPluginLocator(),
            lockPreflightService);

        var result = await launcher.LaunchAsync(
            unityProject,
            new IpcOneshotBootstrapArguments(
                ParentProcessId: 1234,
                ProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
                SessionToken: "session-token",
                ExitDeadlineUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
                EndpointTransportKind: "unixDomainSocket",
                EndpointAddress: "/tmp/ucli.sock"),
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
        var unityProject = ResolvedUnityProjectContextTestFactory.Create(
            unityProjectRoot: "/tmp/unity-project",
            repositoryRoot: "/tmp/repository-root");
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
                        "/tmp/ucli-plugin.json",
                        UnityUcliPluginMarkerContract.ExpectedProtocolVersion));
                },
            },
            lockPreflightService);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            _ = await launcher.LaunchAsync(
                unityProject,
                new IpcOneshotBootstrapArguments(
                    ParentProcessId: 1234,
                    ProjectFingerprint: ProjectFingerprintTestFactory.Create("project-fingerprint"),
                    SessionToken: "session-token",
                    ExitDeadlineUtc: new DateTimeOffset(2026, 03, 09, 0, 0, 0, TimeSpan.Zero),
                    EndpointTransportKind: "unixDomainSocket",
                    EndpointAddress: "/tmp/ucli.sock"),
                scope.GetPath("unity.log"),
                UnityBatchmodeLaunchOptions.Default,
                cancellationTokenSource.Token);
        });

        lockPreflightService.AssertOnlyInitialStartPreflightFor(unityProject, cancellationTokenSource.Token);
    }

}
