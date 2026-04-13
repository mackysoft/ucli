using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests;

public sealed class OperationCatalogProviderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetOperations_WhenUnityProjectIsProvided_UsesProvidedContextWithoutResolvingCurrentDirectory ()
    {
        var unityProject = new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/project",
            RepositoryRoot: "/tmp/repository",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
        var config = UcliConfig.CreateDefault();
        UcliOperationDescriptor[] operations =
        [
            new UcliOperationDescriptor(
                Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen,
                Kind: UcliOperationKind.Query,
                Policy: OperationPolicy.Safe,
                ArgsSchemaJson: """{"type":"object","additionalProperties":false}"""),
        ];
        var contextResolver = new SpyProjectContextResolver();
        var discoveryService = new SpyOperationCatalogDiscoveryService(operations);
        var provider = new OperationCatalogProvider(contextResolver, discoveryService);

        var result = await provider.GetOperations(unityProject, config, failFast: true, cancellationToken: CancellationToken.None);

        Assert.False(contextResolver.WasCalled);
        Assert.Same(unityProject, discoveryService.ReceivedProject);
        Assert.Same(config, discoveryService.ReceivedConfig);
        Assert.Equal(UnityExecutionMode.Auto, discoveryService.ReceivedMode);
        Assert.Null(discoveryService.ReceivedTimeout);
        Assert.True(discoveryService.ReceivedFailFast);
        Assert.Single(result);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen, result[0].Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetOperations_WithoutExplicitUnityProject_ResolvesCurrentDirectoryThenDelegatesToDiscovery ()
    {
        var unityProject = new ResolvedUnityProjectContext(
            UnityProjectRoot: "/tmp/project",
            RepositoryRoot: "/tmp/repository",
            ProjectFingerprint: "project-fingerprint",
            PathSource: UnityProjectPathSource.CommandOption);
        var config = UcliConfig.CreateDefault();
        var contextResolver = new StubProjectContextResolver(ProjectContextResolutionResult.Success(
            new ProjectContext(unityProject, config, ConfigSource.Default)));
        var discoveryService = new SpyOperationCatalogDiscoveryService(
        [
            new UcliOperationDescriptor(
                Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen,
                Kind: UcliOperationKind.Query,
                Policy: OperationPolicy.Safe,
                ArgsSchemaJson: """{"type":"object","additionalProperties":false}"""),
        ]);
        var provider = new OperationCatalogProvider(contextResolver, discoveryService);

        var result = await provider.GetOperations(CancellationToken.None);

        Assert.True(contextResolver.WasCalled);
        Assert.Null(contextResolver.ReceivedProjectPath);
        Assert.Same(unityProject, discoveryService.ReceivedProject);
        Assert.Same(config, discoveryService.ReceivedConfig);
        Assert.False(discoveryService.ReceivedFailFast);
        Assert.Single(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetOperations_WhenCurrentDirectoryContextCannotBeResolved_ThrowsTypedLoadException ()
    {
        var provider = new OperationCatalogProvider(
            new StubProjectContextResolver(ProjectContextResolutionResult.Failure(
                ExecutionError.InvalidArgument("UnityProject is invalid."))),
            new SpyOperationCatalogDiscoveryService([]));

        var exception = await Assert.ThrowsAsync<OperationCatalogLoadException>(async () =>
            await provider.GetOperations(CancellationToken.None));

        Assert.Equal(ExecutionErrorKind.InvalidArgument, exception.Error.Kind);
        Assert.Contains("Operation catalog context could not be resolved.", exception.Error.Message, StringComparison.Ordinal);
    }

    private sealed class SpyProjectContextResolver : IProjectContextResolver
    {
        public bool WasCalled { get; private set; }

        public ValueTask<ProjectContextResolutionResult> Resolve (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ProjectContextResolutionResult.Failure(
                ExecutionError.InternalError("Should not be called.")));
        }
    }

    private sealed class StubProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContextResolutionResult result;

        public StubProjectContextResolver (ProjectContextResolutionResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public bool WasCalled { get; private set; }

        public string? ReceivedProjectPath { get; private set; }

        public ValueTask<ProjectContextResolutionResult> Resolve (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            ReceivedProjectPath = projectPath;
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SpyOperationCatalogDiscoveryService : IOperationCatalogDiscoveryService
    {
        private readonly IReadOnlyList<UcliOperationDescriptor> operations;

        public SpyOperationCatalogDiscoveryService (IReadOnlyList<UcliOperationDescriptor> operations)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        public ResolvedUnityProjectContext? ReceivedProject { get; private set; }

        public UcliConfig? ReceivedConfig { get; private set; }

        public UnityExecutionMode ReceivedMode { get; private set; }

        public TimeSpan? ReceivedTimeout { get; private set; }

        public bool ReceivedFailFast { get; private set; }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> Discover (
            ResolvedUnityProjectContext unityProject,
            UcliConfig config,
            UnityExecutionMode mode = UnityExecutionMode.Auto,
            TimeSpan? timeout = null,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedProject = unityProject;
            ReceivedConfig = config;
            ReceivedMode = mode;
            ReceivedTimeout = timeout;
            ReceivedFailFast = failFast;
            return ValueTask.FromResult(operations);
        }
    }
}