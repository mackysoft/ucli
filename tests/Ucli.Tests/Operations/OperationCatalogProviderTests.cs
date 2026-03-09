using System.Text.Json;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.Ops;
using MackySoft.Ucli.ReadIndex;
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
        var config = new UcliConfig(
            SchemaVersion: UcliContractConstants.Config.SchemaVersion,
            OperationPolicy: OperationPolicy.Safe,
            PlanTokenMode: PlanTokenMode.Optional,
            ReadIndexDefaultMode: ReadIndexMode.RequireFresh,
            OperationAllowlist: Array.Empty<string>());
        var contextResolver = new SpyProjectContextResolver();
        var catalogReader = new SpyOpsCatalogReader();
        var provider = new OperationCatalogProvider(contextResolver, catalogReader);

        var operations = await provider.GetOperations(unityProject, config, CancellationToken.None);

        Assert.False(contextResolver.WasCalled);
        Assert.Same(unityProject, catalogReader.ReceivedProject);
        Assert.Same(config, catalogReader.ReceivedConfig);
        Assert.Single(operations);
        Assert.Equal("ucli.scene.open", operations[0].Name);
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

    private sealed class SpyOpsCatalogReader : IOpsCatalogReader
    {
        public ResolvedUnityProjectContext? ReceivedProject { get; private set; }

        public UcliConfig? ReceivedConfig { get; private set; }

        public ValueTask<OpsCatalogFetchResult> Read (
            ResolvedUnityProjectContext project,
            UcliConfig config,
            string? mode,
            string? timeout,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReceivedProject = project;
            ReceivedConfig = config;
            Assert.Null(mode);
            Assert.Null(timeout);

            return ValueTask.FromResult(OpsCatalogFetchResult.Success(new IpcOpsReadResponse(
                GeneratedAtUtc: DateTimeOffset.UtcNow,
                Operations:
                [
                    new IndexOpEntryJsonContract(
                        Name: "ucli.scene.open",
                        Kind: "query",
                        Policy: "safe",
                        ArgsSchemaJson: JsonSerializer.Serialize(new
                        {
                            type = "object",
                            additionalProperties = false,
                        })),
                ])));
        }
    }
}
