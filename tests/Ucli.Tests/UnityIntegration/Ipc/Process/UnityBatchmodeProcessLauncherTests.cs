using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Process;
using MackySoft.Ucli.UnityIntegration.Ipc.Transport;
using MackySoft.Ucli.UnityIntegration.Project.Plugin;
using MackySoft.Ucli.UnityIntegration.Resolution;

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
            SessionToken: "session-token",
            EndpointTransportKind: IpcTransportKindValues.UnixDomainSocket,
            EndpointAddress: @"\\.\pipe\ucli-oneshot");

        var arguments = UnityBatchmodeProcessLauncher.BuildArgumentTokens(projectPath, unityLogPath, bootstrapArguments);

        Assert.Equal("-projectPath", arguments[2]);
        Assert.Equal(projectPath, arguments[3]);
        Assert.Equal("-logFile", arguments[4]);
        Assert.Equal(unityLogPath, arguments[5]);
        Assert.DoesNotContain(@"C:\\Users\\Foo Bar\\Project", arguments, StringComparer.Ordinal);
        Assert.DoesNotContain(@"C:\\Users\\Foo Bar\\Project\\.ucli\\unity.log", arguments, StringComparer.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Launch_WhenUnityPluginMarkerIsMissing_ReturnsInvalidArgumentWithoutResolvingUnityVersion ()
    {
        var versionResolver = new StubUnityVersionResolver();
        var launcher = new UnityBatchmodeProcessLauncher(
            versionResolver,
            new StubUnityEditorPathResolver(),
            new StubIpcEndpointResolver(),
            new StubUnityUcliPluginLocator
            {
                Result = UnityUcliPluginLocateResult.NotFound(ExecutionError.InvalidArgument(
                    "Unity project does not contain the uCLI Unity plugin.")),
            });

        var result = await launcher.Launch(
            new ResolvedUnityProjectContext(
                UnityProjectRoot: "/tmp/unity-project",
                RepositoryRoot: "/tmp/repository-root",
                ProjectFingerprint: "project-fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            new IpcOneshotBootstrapArguments(
                ParentProcessId: 1234,
                SessionToken: "session-token",
                EndpointTransportKind: IpcTransportKindValues.UnixDomainSocket,
                EndpointAddress: "/tmp/ucli.sock"),
            "/tmp/unity.log",
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, error.Kind);
        Assert.Equal(0, versionResolver.CallCount);
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
        public UnityEditorPathResolutionResult Resolve (
            string unityVersion,
            string? preferredUnityEditorPath)
        {
            return UnityEditorPathResolutionResult.Success("/Applications/Unity.app/Contents/MacOS/Unity");
        }
    }

    private sealed class StubIpcEndpointResolver : IIpcEndpointResolver
    {
        public IpcEndpoint Resolve (
            string repositoryRoot,
            string projectFingerprint)
        {
            return new IpcEndpoint(IpcTransportKind.UnixDomainSocket, "/tmp/ucli.sock");
        }
    }

    private sealed class StubUnityUcliPluginLocator : IUnityUcliPluginLocator
    {
        public UnityUcliPluginLocateResult Result { get; set; }
            = UnityUcliPluginLocateResult.Found(
                "/tmp/ucli-plugin.json",
                UnityUcliPluginLocator.ExpectedProtocolVersion);

        public ValueTask<UnityUcliPluginLocateResult> Locate (
            string unityProjectRoot,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result);
        }
    }
}
