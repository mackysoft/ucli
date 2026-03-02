namespace MackySoft.Ucli.Tests;

using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Operations;

public sealed class OperationCatalogTests
{
    private const string ArgsSchemaJson = """{"type":"object"}""";

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_ReturnsRegisteredOperation ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.Get("ucli.scene.open", CancellationToken.None);

        Assert.NotNull(descriptor);
        Assert.Equal("ucli.scene.open", descriptor.Name);
        Assert.Equal(UcliOperationKind.Query, descriptor.Kind);
        Assert.Equal(OperationPolicy.Safe, descriptor.Policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetAll_ReturnsOperationsOrderedByName ()
    {
        var provider = new TestOperationCatalogProvider(
        [
            new UcliOperationDescriptor("ucli.z", UcliOperationKind.Query, OperationPolicy.Safe, ArgsSchemaJson),
            new UcliOperationDescriptor("ucli.a", UcliOperationKind.Query, OperationPolicy.Safe, ArgsSchemaJson),
            new UcliOperationDescriptor("ucli.m", UcliOperationKind.Query, OperationPolicy.Safe, ArgsSchemaJson),
        ]);
        var catalog = new OperationCatalog(provider);

        var listed = await catalog.GetAll(CancellationToken.None);

        Assert.Equal(3, listed.Count);
        Assert.Equal("ucli.a", listed[0].Name);
        Assert.Equal("ucli.m", listed[1].Name);
        Assert.Equal("ucli.z", listed[2].Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task GetAll_ThrowsInvalidOperationException_WhenOperationNameIsDuplicated ()
    {
        var provider = new TestOperationCatalogProvider(
        [
            new UcliOperationDescriptor("ucli.scene.open", UcliOperationKind.Query, OperationPolicy.Safe, ArgsSchemaJson),
            new UcliOperationDescriptor("ucli.scene.open", UcliOperationKind.Query, OperationPolicy.Safe, ArgsSchemaJson),
        ]);
        var catalog = new OperationCatalog(provider);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await catalog.GetAll(CancellationToken.None));
        Assert.Contains("duplicated", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_ReturnsNull_WhenOperationDoesNotExist ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var found = await catalog.Get("ucli.unknown.operation", CancellationToken.None);

        Assert.Null(found);
    }

    private sealed class TestOperationCatalogProvider : IOperationCatalogProvider
    {
        private readonly IReadOnlyList<UcliOperationDescriptor> operations;

        public TestOperationCatalogProvider (IReadOnlyList<UcliOperationDescriptor> operations)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(operations);
        }
    }
}