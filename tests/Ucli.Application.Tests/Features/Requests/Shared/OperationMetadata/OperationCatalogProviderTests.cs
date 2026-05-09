using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

public sealed class OperationCatalogProviderTests
{
    private static readonly UcliErrorCode CustomOperationMetadataErrorCode = new("OPERATION_METADATA_CUSTOM_ERROR");

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

        var result = await provider.GetOperationsAsync(unityProject, config, failFast: true, cancellationToken: CancellationToken.None);

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

        var result = await provider.GetOperationsAsync(CancellationToken.None);

        Assert.True(contextResolver.WasCalled);
        Assert.Null(contextResolver.ReceivedProjectPath);
        Assert.Same(unityProject, discoveryService.ReceivedProject);
        Assert.Same(config, discoveryService.ReceivedConfig);
        Assert.False(discoveryService.ReceivedFailFast);
        Assert.Single(result);
    }

    public static TheoryData<int, UcliErrorCode> ContextResolutionErrorCases => new()
    {
        { (int)ExecutionErrorKind.InvalidArgument, ProjectContextErrorCodes.UnityProjectMarkerMissing },
        { (int)ExecutionErrorKind.Timeout, ExecutionErrorCodes.IpcTimeout },
        { (int)ExecutionErrorKind.InternalError, CustomOperationMetadataErrorCode },
    };

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(ContextResolutionErrorCases))]
    public async Task GetOperations_WhenCurrentDirectoryContextCannotBeResolved_ThrowsTypedLoadException (
        int errorKindValue,
        UcliErrorCode errorCode)
    {
        var errorKind = (ExecutionErrorKind)errorKindValue;
        var provider = new OperationCatalogProvider(
            new StubProjectContextResolver(ProjectContextResolutionResult.Failure(
                CreateError(
                    errorKind,
                    "UnityProject is invalid.",
                    errorCode))),
            new SpyOperationCatalogDiscoveryService([]));

        var exception = await Assert.ThrowsAsync<OperationCatalogLoadException>(async () =>
            await provider.GetOperationsAsync(CancellationToken.None));

        Assert.Equal(errorKind, exception.Error.Kind);
        Assert.Equal(errorCode, exception.Error.Code);
        Assert.Equal(errorCode, exception.ErrorCode);
        Assert.Contains("Operation catalog context could not be resolved.", exception.Error.Message, StringComparison.Ordinal);
    }

    private sealed class SpyProjectContextResolver : IProjectContextResolver
    {
        public bool WasCalled { get; private set; }

        public ValueTask<ProjectContextResolutionResult> ResolveAsync (
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

        public ValueTask<ProjectContextResolutionResult> ResolveAsync (
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

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> DiscoverAsync (
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

    private static ExecutionError CreateError (
        ExecutionErrorKind errorKind,
        string message,
        UcliErrorCode errorCode)
    {
        return errorKind switch
        {
            ExecutionErrorKind.InvalidArgument => ExecutionError.InvalidArgument(message, errorCode),
            ExecutionErrorKind.Timeout => ExecutionError.Timeout(message, errorCode),
            ExecutionErrorKind.InternalError => ExecutionError.InternalError(message, errorCode),
            _ => throw new ArgumentOutOfRangeException(nameof(errorKind), errorKind, "Unsupported execution error kind."),
        };
    }
}
