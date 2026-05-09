namespace MackySoft.Ucli.Application.Tests;

using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

public sealed class OperationCatalogTests
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
    public async Task Get_WhenOperationIsResolve_ReturnsSelectorSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(UcliPrimitiveOperationNames.Resolve, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var schemaRoot = schemaDocument.RootElement;
        Assert.Equal(JsonValueKind.Object, schemaRoot.ValueKind);
        Assert.True(schemaRoot.TryGetProperty("additionalProperties", out var additionalProperties));
        Assert.False(additionalProperties.GetBoolean());
        Assert.False(schemaRoot.TryGetProperty("oneOf", out _));
        Assert.False(schemaRoot.TryGetProperty("allOf", out _));
        var properties = schemaRoot.GetProperty("properties");
        Assert.True(properties.TryGetProperty("globalObjectId", out _));
        Assert.True(properties.TryGetProperty("assetGuid", out _));
        Assert.True(properties.TryGetProperty("assetPath", out _));
        Assert.True(properties.TryGetProperty("projectAssetPath", out _));
        Assert.True(properties.TryGetProperty("scene", out _));
        Assert.True(properties.TryGetProperty("prefab", out _));
        Assert.True(properties.TryGetProperty("hierarchyPath", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsSceneTree_ReturnsDepthSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(UcliPrimitiveOperationNames.SceneTree, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var schemaRoot = schemaDocument.RootElement;
        Assert.Equal(JsonValueKind.Object, schemaRoot.ValueKind);
        Assert.True(schemaRoot.TryGetProperty("additionalProperties", out var additionalProperties));
        Assert.False(additionalProperties.GetBoolean());
        Assert.True(schemaRoot.TryGetProperty("required", out var required));
        Assert.True(ContainsArrayLiteral(required, "path"));
        Assert.True(schemaRoot.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("path", out _));
        Assert.True(properties.TryGetProperty("depth", out var depthProperty));
        Assert.True(depthProperty.TryGetProperty("type", out var depthType));
        Assert.Equal(JsonValueKind.Array, depthType.ValueKind);
        Assert.True(ContainsArrayLiteral(depthType, "integer"));
        Assert.True(ContainsArrayLiteral(depthType, "null"));
        Assert.False(depthProperty.TryGetProperty("minimum", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsGoCreate_ReturnsSceneOrParentSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(UcliPrimitiveOperationNames.GoCreate, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var schemaRoot = schemaDocument.RootElement;
        Assert.Equal(JsonValueKind.Object, schemaRoot.ValueKind);
        Assert.True(schemaRoot.TryGetProperty("required", out var required));
        Assert.True(ContainsArrayLiteral(required, "name"));
        Assert.True(schemaRoot.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("scene", out _));
        Assert.True(properties.TryGetProperty("parent", out var parentProperty));
        Assert.True(parentProperty.GetProperty("properties").TryGetProperty("prefab", out _));
        Assert.False(parentProperty.TryGetProperty("oneOf", out _));
        Assert.False(schemaRoot.TryGetProperty("oneOf", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsGoDescribe_ReturnsTargetAndDepthSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(UcliPrimitiveOperationNames.GoDescribe, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var schemaRoot = schemaDocument.RootElement;
        Assert.Equal(JsonValueKind.Object, schemaRoot.ValueKind);
        Assert.True(schemaRoot.TryGetProperty("required", out var required));
        Assert.True(ContainsArrayLiteral(required, "target"));
        Assert.True(schemaRoot.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("target", out var targetProperty));
        Assert.False(targetProperty.TryGetProperty("oneOf", out _));
        Assert.True(properties.TryGetProperty("depth", out var depthProperty));
        Assert.True(depthProperty.TryGetProperty("type", out var depthType));
        Assert.Equal(JsonValueKind.Array, depthType.ValueKind);
        Assert.True(ContainsArrayLiteral(depthType, "integer"));
        Assert.True(ContainsArrayLiteral(depthType, "null"));
        Assert.False(depthProperty.TryGetProperty("minimum", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsCompSet_ReturnsComponentSelectorAndMinItemsSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(UcliPrimitiveOperationNames.CompSet, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var properties = schemaDocument.RootElement.GetProperty("properties");
        var targetProperty = properties.GetProperty("target");
        Assert.True(targetProperty.GetProperty("properties").TryGetProperty("componentType", out _));
        Assert.True(targetProperty.GetProperty("properties").TryGetProperty("prefab", out _));
        Assert.False(targetProperty.TryGetProperty("oneOf", out _));
        Assert.False(properties.GetProperty("sets").TryGetProperty("minItems", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsAssetSet_ReturnsAssetSelectorAndMinItemsSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(UcliPrimitiveOperationNames.AssetSet, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var properties = schemaDocument.RootElement.GetProperty("properties");
        var targetProperty = properties.GetProperty("target");
        Assert.True(targetProperty.GetProperty("properties").TryGetProperty("projectAssetPath", out _));
        Assert.False(targetProperty.TryGetProperty("oneOf", out _));
        Assert.False(properties.GetProperty("sets").TryGetProperty("minItems", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsAssetSchema_ReturnsTypeOrTargetSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(UcliPrimitiveOperationNames.AssetSchema, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var root = schemaDocument.RootElement;
        Assert.False(root.TryGetProperty("oneOf", out _));
        var properties = root.GetProperty("properties");
        Assert.True(properties.TryGetProperty("type", out _));
        Assert.True(properties.GetProperty("target").GetProperty("properties").TryGetProperty("projectAssetPath", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsAssetsFind_ReturnsRegisteredDescriptorWithOptionalFilters ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(UcliPrimitiveOperationNames.AssetsFind, CancellationToken.None);

        Assert.NotNull(descriptor);
        Assert.Equal(UcliPrimitiveOperationNames.AssetsFind, descriptor.Name);
        Assert.Equal(UcliOperationKind.Query, descriptor.Kind);
        Assert.Equal(OperationPolicy.Safe, descriptor.Policy);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var root = schemaDocument.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.False(root.TryGetProperty("minProperties", out _));
        var properties = root.GetProperty("properties");
        Assert.True(properties.TryGetProperty("type", out _));
        Assert.True(properties.TryGetProperty("pathPrefix", out _));
        Assert.True(properties.TryGetProperty("nameContains", out _));
        Assert.False(root.TryGetProperty("required", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsCompSchema_ReturnsTypeOnlySchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(UcliPrimitiveOperationNames.CompSchema, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var root = schemaDocument.RootElement;
        Assert.True(root.GetProperty("properties").TryGetProperty("type", out _));
        Assert.True(root.TryGetProperty("required", out var required));
        Assert.True(ContainsArrayLiteral(required, "type"));
        Assert.False(root.GetProperty("properties").TryGetProperty("target", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsGoDelete_ReturnsPublicTargetSelectorSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(UcliPrimitiveOperationNames.GoDelete, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var targetProperty = schemaDocument.RootElement.GetProperty("properties").GetProperty("target");
        Assert.True(targetProperty.GetProperty("properties").TryGetProperty("prefab", out _));
        Assert.False(targetProperty.TryGetProperty("oneOf", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsGoReparent_ReturnsPublicTargetAndParentSelectorSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(UcliPrimitiveOperationNames.GoReparent, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var properties = schemaDocument.RootElement.GetProperty("properties");
        Assert.False(properties.GetProperty("target").TryGetProperty("oneOf", out _));
        Assert.False(properties.GetProperty("parent").TryGetProperty("oneOf", out _));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsPrefabCreate_ReturnsSceneOnlyTargetSelectorSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(UcliPrimitiveOperationNames.PrefabCreate, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var targetProperty = schemaDocument.RootElement.GetProperty("properties").GetProperty("target");
        Assert.False(targetProperty.GetProperty("properties").TryGetProperty("prefab", out _));
        Assert.False(targetProperty.TryGetProperty("oneOf", out _));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UcliPrimitiveOperationNames.ProjectRefresh)]
    [InlineData(UcliPrimitiveOperationNames.ProjectSave)]
    public async Task Get_WhenOperationIsProjectMutation_ReturnsStrictEmptyObjectSchema (string operationName)
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.GetAsync(operationName, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var schemaRoot = schemaDocument.RootElement;
        Assert.Equal(JsonValueKind.Object, schemaRoot.ValueKind);
        Assert.True(schemaRoot.TryGetProperty("type", out var typeProperty));
        Assert.Equal("object", typeProperty.GetString());
        Assert.True(schemaRoot.TryGetProperty("additionalProperties", out var additionalProperties));
        Assert.False(additionalProperties.GetBoolean());
        if (schemaRoot.TryGetProperty("properties", out var properties))
        {
            Assert.Empty(properties.EnumerateObject());
        }
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
        var provider = new TestOperationCatalogProvider(
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

    private sealed class TestOperationCatalogProvider : IOperationCatalogProvider
    {
        private readonly IReadOnlyList<UcliOperationDescriptor> operations;

        public TestOperationCatalogProvider (IReadOnlyList<UcliOperationDescriptor> operations)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperationsAsync (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(operations);
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperationsAsync (
            ResolvedUnityProjectContext unityProject,
            UcliConfig config,
            UnityExecutionMode mode = UnityExecutionMode.Auto,
            TimeSpan? timeout = null,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(unityProject);
            ArgumentNullException.ThrowIfNull(config);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(operations);
        }
    }

    private static bool ContainsArrayLiteral (
        JsonElement arrayElement,
        string literal)
    {
        foreach (var element in arrayElement.EnumerateArray())
        {
            if (string.Equals(element.GetString(), literal, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
