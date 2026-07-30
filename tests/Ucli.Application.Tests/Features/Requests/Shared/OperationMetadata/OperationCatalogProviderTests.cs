using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

public sealed class OperationCatalogProviderTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task GetOperations_WhenUnityProjectIsProvided_UsesProvidedContextWithoutResolvingCurrentDirectory ()
    {
        var unityProject = ProjectContextTestFactory.CreateTemporaryFixtureUnityProject();
        var config = UcliConfig.CreateDefault();
        UcliOperationDescriptor[] operations =
        [
            new UcliOperationDescriptor(
                Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen,
                Kind: UcliOperationKind.Query,
                Policy: OperationPolicy.Safe,
                ArgsSchemaJson: """{"type":"object","additionalProperties":false}""",
                DescriptorDigest: Sha256DigestTestFactory.Compute("scene open"),
                VerdictContract: null,
                ResultSchemaJson: null,
                Exposure: UcliOperationExposure.Public),
        ];
        var contextResolver = new UnexpectedProjectContextResolver();
        var discoveryService = new RecordingOperationCatalogDiscoveryService(operations);
        var provider = new OperationCatalogProvider(contextResolver, discoveryService);

        var result = await provider.GetOperationsAsync(
            unityProject,
            config,
            mode: UnityExecutionMode.Auto,
            timeout: null,
            failFast: true,
            cancellationToken: CancellationToken.None);

        OperationCatalogInvocationAssert.OperationDiscoveryRequestedOnce(
            discoveryService,
            unityProject,
            config,
            UnityExecutionMode.Auto,
            expectedTimeout: null,
            expectedFailFast: true);
        Assert.Single(result);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen, result[0].Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetOperations_WithoutExplicitUnityProject_ResolvesCurrentDirectoryThenDelegatesToDiscovery ()
    {
        var config = UcliConfig.CreateDefault();
        var context = ProjectContextTestFactory.CreateTemporaryFixtureProject(config);
        var contextResolver = new StaticProjectContextResolver(ProjectContextResolutionResult.Success(
            context));
        var discoveryService = new RecordingOperationCatalogDiscoveryService(
        [
            new UcliOperationDescriptor(
                Name: MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen,
                Kind: UcliOperationKind.Query,
                Policy: OperationPolicy.Safe,
                ArgsSchemaJson: """{"type":"object","additionalProperties":false}""",
                DescriptorDigest: Sha256DigestTestFactory.Compute("scene open"),
                VerdictContract: null,
                ResultSchemaJson: null,
                Exposure: UcliOperationExposure.Public),
        ]);
        var provider = new OperationCatalogProvider(contextResolver, discoveryService);

        var result = await provider.GetOperationsAsync(CancellationToken.None);

        ProjectContextResolverAssert.ProjectContextResolvedOnce(
            contextResolver,
            expectedProjectPath: null,
            CancellationToken.None);
        OperationCatalogInvocationAssert.OperationDiscoveryRequestedOnce(
            discoveryService,
            context.UnityProject,
            config,
            UnityExecutionMode.Auto,
            expectedTimeout: null,
            expectedFailFast: false);
        Assert.Single(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetOperations_WhenCurrentDirectoryContextCannotBeResolved_ThrowsTypedLoadException ()
    {
        var provider = new OperationCatalogProvider(
            new StaticProjectContextResolver(ProjectContextResolutionResult.Failure(
                ExecutionError.InvalidArgument(
                    "UnityProject is invalid.",
                    ProjectContextErrorCodes.UnityProjectMarkerMissing))),
            new RecordingOperationCatalogDiscoveryService([]));

        var exception = await Assert.ThrowsAsync<OperationCatalogLoadException>(async () =>
            await provider.GetOperationsAsync(CancellationToken.None));

        Assert.Equal(ApplicationFailureKind.InvalidInput, exception.Error.Kind);
        Assert.Equal(ProjectContextErrorCodes.UnityProjectMarkerMissing, exception.Error.Code);
        Assert.Contains("Operation catalog context could not be resolved.", exception.Error.Message, StringComparison.Ordinal);
    }
}
