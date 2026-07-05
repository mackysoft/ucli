namespace MackySoft.Ucli.Application.Tests;

using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

public sealed class OperationCatalogLookupTests
{
    private const string ArgsSchemaJson = """{"type":"object"}""";

    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_ReturnsRegisteredOperation ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(UcliPrimitiveOperationNames.SceneOpen, CancellationToken.None);

        Assert.NotNull(descriptor);
        Assert.Equal(UcliPrimitiveOperationNames.SceneOpen, descriptor.Name);
        Assert.Equal(UcliOperationKind.Command, descriptor.Kind);
        Assert.Equal(OperationPolicy.Safe, descriptor.Policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetAll_ReturnsOperationsOrderedByName ()
    {
        var provider = new InMemoryOperationCatalogProvider(
        [
            new UcliOperationDescriptor("ucli.z", UcliOperationKind.Query, OperationPolicy.Safe, ArgsSchemaJson),
            new UcliOperationDescriptor("ucli.a", UcliOperationKind.Query, OperationPolicy.Safe, ArgsSchemaJson),
            new UcliOperationDescriptor("ucli.m", UcliOperationKind.Query, OperationPolicy.Safe, ArgsSchemaJson),
        ]);
        var catalog = new OperationCatalog(provider);

        var listed = await catalog.GetAllAsync(CancellationToken.None);

        Assert.Equal(3, listed.Count);
        Assert.Equal("ucli.a", listed[0].Name);
        Assert.Equal("ucli.m", listed[1].Name);
        Assert.Equal("ucli.z", listed[2].Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetAll_ThrowsInvalidOperationException_WhenOperationNameIsDuplicated ()
    {
        var provider = new InMemoryOperationCatalogProvider(
        [
            new UcliOperationDescriptor(UcliPrimitiveOperationNames.SceneOpen, UcliOperationKind.Command, OperationPolicy.Safe, ArgsSchemaJson),
            new UcliOperationDescriptor(UcliPrimitiveOperationNames.SceneOpen, UcliOperationKind.Command, OperationPolicy.Safe, ArgsSchemaJson),
        ]);
        var catalog = new OperationCatalog(provider);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                catalog.GetAllAsync(CancellationToken.None).AsTask(),
                "Duplicate operation catalog load",
                AsyncWaitTimeout);
        });
        Assert.Contains("duplicated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_ReturnsNull_WhenOperationDoesNotExist ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var found = await catalog.GetAsync("ucli.unknown.operation", CancellationToken.None);

        Assert.Null(found);
    }
}
