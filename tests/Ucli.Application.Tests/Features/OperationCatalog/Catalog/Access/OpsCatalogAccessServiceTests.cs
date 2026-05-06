using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests.Ops.Access;

public sealed class OpsCatalogAccessServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenAllowStaleIndexExists_ReturnsPersistedCatalog ()
    {
        var persistedReader = new StubPersistedOpsCatalogReader
        {
            Result = PersistedOpsCatalogReadResult.Success(
                [CreateGoDescribeEntry()],
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Probable),
        };
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService();
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.Read(CreatePreflightContext(ReadIndexMode.AllowStale), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Index, result.Output!.AccessInfo.Source);
        Assert.True(result.Output.AccessInfo.Used);
        Assert.True(result.Output.AccessInfo.Hit);
        Assert.Equal(IndexFreshness.Probable, result.Output.AccessInfo.Freshness);
        Assert.Equal(0, sourceRefreshService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenRequireFreshIndexIsStale_FallsBackToSource ()
    {
        var persistedReader = new StubPersistedOpsCatalogReader
        {
            Result = PersistedOpsCatalogReadResult.Success(
                [CreateGoDescribeEntry()],
                DateTimeOffset.Parse("2026-03-06T00:00:00+00:00"),
                IndexFreshness.Stale),
        };
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService
        {
            Result = OpsCatalogSourceRefreshResult.Success([CreateSceneSaveEntry()], generatedAtUtc, "Existing ops index freshness is 'stale'."),
        };
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.Read(CreatePreflightContext(ReadIndexMode.RequireFresh), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
        Assert.False(result.Output.AccessInfo.Used);
        Assert.Equal(IndexFreshness.Fresh, result.Output.AccessInfo.Freshness);
        Assert.Equal(generatedAtUtc, result.Output.AccessInfo.GeneratedAtUtc);
        Assert.Contains("stale", result.Output.AccessInfo.FallbackReason, StringComparison.Ordinal);
        Assert.Equal(1, sourceRefreshService.CallCount);
        Assert.Equal("Existing ops index freshness is 'stale'.", sourceRefreshService.LastFallbackReason);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenPersistedReadReturnsInvalidArgument_ReturnsFailureWithoutSourceFallback ()
    {
        var persistedReader = new StubPersistedOpsCatalogReader
        {
            Result = PersistedOpsCatalogReadResult.Failure(IpcErrorCodes.InvalidArgument, "invalid project fingerprint"),
        };
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService();
        var service = new OpsCatalogAccessService(persistedReader, sourceRefreshService);

        var result = await service.Read(CreatePreflightContext(ReadIndexMode.AllowStale), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(IpcErrorCodes.InvalidArgument, result.ErrorCode);
        Assert.Equal(0, sourceRefreshService.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Read_WhenReadIndexDisabled_UsesSourceRefreshWithDisabledFallbackReason ()
    {
        var generatedAtUtc = DateTimeOffset.Parse("2026-03-07T00:00:00+00:00");
        var sourceRefreshService = new StubOpsCatalogSourceRefreshService
        {
            Result = OpsCatalogSourceRefreshResult.Success([CreateGoDescribeEntry()], generatedAtUtc, "readIndex disabled by mode."),
        };
        var service = new OpsCatalogAccessService(new StubPersistedOpsCatalogReader(), sourceRefreshService);

        var result = await service.Read(CreatePreflightContext(ReadIndexMode.Disabled), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(OpsCatalogSource.Source, result.Output!.AccessInfo.Source);
        Assert.Equal("readIndex disabled by mode.", sourceRefreshService.LastFallbackReason);
        Assert.Equal(1, sourceRefreshService.CallCount);
    }

    private static OpsPreflightContext CreatePreflightContext (ReadIndexMode readIndexMode)
    {
        return new OpsPreflightContext(
            new ProjectContext(
                new ResolvedUnityProjectContext(
                    UnityProjectRoot: "/repo/UnityProject",
                    RepositoryRoot: "/repo",
                    ProjectFingerprint: "project-fingerprint",
                    PathSource: UnityProjectPathSource.CommandOption),
                UcliConfig.CreateDefault(),
                ConfigSource.Default),
            readIndexMode,
            UnityExecutionMode.Auto,
            TimeSpan.FromMilliseconds(1200),
            true);
    }

    private static IndexOpEntryJsonContract CreateGoDescribeEntry ()
    {
        return new IndexOpEntryJsonContract(
            Name: UcliPrimitiveOperationNames.GoDescribe,
            Kind: "query",
            Policy: "safe",
            ArgsSchemaJson: """{"type":"object"}""",
            ResultSchemaJson: """{"type":"object"}""")
        {
            Description = "Returns a GameObject description including components and child hierarchy.",
            Inputs = Array.Empty<UcliOperationInputContract>(),
            ResultContract = UcliOperationResultContract.One<GameObjectDescriptionResult>("GameObject description result."),
            Assurance = new UcliOperationAssuranceContract(
                Array.Empty<string>(),
                mayDirty: false,
                mayPersist: false,
                Array.Empty<string>(),
                UcliOperationPlanModeValues.ObservesLiveUnity),
        };
    }

    private static IndexOpEntryJsonContract CreateSceneSaveEntry ()
    {
        return new IndexOpEntryJsonContract(
            Name: UcliPrimitiveOperationNames.SceneSave,
            Kind: "mutation",
            Policy: "advanced",
            ArgsSchemaJson: """{"type":"object"}""")
        {
            Description = "Saves a Unity scene asset.",
            Inputs = Array.Empty<UcliOperationInputContract>(),
            ResultContract = UcliOperationResultContract.NoResult("No operation-specific result is emitted."),
            Assurance = new UcliOperationAssuranceContract(
                Array.Empty<string>(),
                mayDirty: false,
                mayPersist: true,
                Array.Empty<string>(),
                UcliOperationPlanModeValues.ObservesLiveUnity),
        };
    }

    private sealed class StubPersistedOpsCatalogReader : IPersistedOpsCatalogReader
    {
        public PersistedOpsCatalogReadResult Result { get; set; }
            = PersistedOpsCatalogReadResult.Failure(
                IpcErrorCodes.ReadIndexBootstrapFailed,
                "Index contract file was not found: ops.catalog.json.");

        public ValueTask<PersistedOpsCatalogReadResult> Read (
            ResolvedUnityProjectContext unityProject,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(unityProject);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Result);
        }
    }

    private sealed class StubOpsCatalogSourceRefreshService : IOpsCatalogSourceRefreshService
    {
        public int CallCount { get; private set; }

        public string? LastFallbackReason { get; private set; }

        public OpsCatalogSourceRefreshResult Result { get; set; }
            = OpsCatalogSourceRefreshResult.Failure("not configured", IpcErrorCodes.InternalError);

        public ValueTask<OpsCatalogSourceRefreshResult> Refresh (
            OpsPreflightContext context,
            string fallbackReason,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            LastFallbackReason = fallbackReason;
            return ValueTask.FromResult(Result);
        }
    }
}
