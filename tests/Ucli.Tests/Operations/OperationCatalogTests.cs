namespace MackySoft.Ucli.Tests;

using System.Text.Json;
using MackySoft.Tests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.UnityProject;

public sealed class OperationCatalogTests
{
    private const string ArgsSchemaJson = """{"type":"object"}""";

    private static readonly TimeSpan AsyncWaitTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_ReturnsRegisteredOperation ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.Get(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen, CancellationToken.None);

        Assert.NotNull(descriptor);
        Assert.Equal(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen, descriptor.Name);
        Assert.Equal(UcliOperationKind.Query, descriptor.Kind);
        Assert.Equal(OperationPolicy.Safe, descriptor.Policy);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsResolve_ReturnsSelectorSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.Get(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.Resolve, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var schemaRoot = schemaDocument.RootElement;
        Assert.Equal(JsonValueKind.Object, schemaRoot.ValueKind);
        Assert.True(schemaRoot.TryGetProperty("additionalProperties", out var additionalProperties));
        Assert.False(additionalProperties.GetBoolean());
        Assert.True(schemaRoot.TryGetProperty("oneOf", out var oneOf));
        Assert.Equal(JsonValueKind.Array, oneOf.ValueKind);
        Assert.Equal(4, oneOf.GetArrayLength());
        Assert.True(ContainsRequiredProperty(oneOf, "globalObjectId"));
        Assert.True(ContainsRequiredProperty(oneOf, "assetGuid"));
        Assert.True(ContainsRequiredProperty(oneOf, "assetPath"));
        Assert.True(ContainsRequiredProperties(oneOf, "scene", "hierarchyPath"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsSceneTree_ReturnsDepthSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.Get(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneTree, CancellationToken.None);

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
        Assert.True(depthProperty.TryGetProperty("minimum", out var depthMinimum));
        Assert.Equal(0, depthMinimum.GetInt32());
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsGoCreate_ReturnsSceneOrParentSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.Get(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoCreate, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var schemaRoot = schemaDocument.RootElement;
        Assert.Equal(JsonValueKind.Object, schemaRoot.ValueKind);
        Assert.True(schemaRoot.TryGetProperty("required", out var required));
        Assert.True(ContainsArrayLiteral(required, "name"));
        Assert.True(schemaRoot.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("scene", out _));
        Assert.True(properties.TryGetProperty("parent", out var parentProperty));
        Assert.True(parentProperty.TryGetProperty("oneOf", out var parentOneOf));
        Assert.Equal(3, parentOneOf.GetArrayLength());
        Assert.True(ContainsRequiredProperty(parentOneOf, "var"));
        Assert.True(ContainsRequiredProperty(parentOneOf, "globalObjectId"));
        Assert.True(ContainsRequiredProperties(parentOneOf, "scene", "hierarchyPath"));
        Assert.True(schemaRoot.TryGetProperty("oneOf", out var rootOneOf));
        Assert.Equal(2, rootOneOf.GetArrayLength());
        Assert.True(ContainsRequiredProperty(rootOneOf, "scene"));
        Assert.True(ContainsRequiredProperty(rootOneOf, "parent"));
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Get_WhenOperationIsGoDescribe_ReturnsTargetAndDepthSchema ()
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.Get(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.GoDescribe, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var schemaRoot = schemaDocument.RootElement;
        Assert.Equal(JsonValueKind.Object, schemaRoot.ValueKind);
        Assert.True(schemaRoot.TryGetProperty("required", out var required));
        Assert.True(ContainsArrayLiteral(required, "target"));
        Assert.True(schemaRoot.TryGetProperty("properties", out var properties));
        Assert.True(properties.TryGetProperty("target", out var targetProperty));
        Assert.True(targetProperty.TryGetProperty("oneOf", out var targetOneOf));
        Assert.Equal(3, targetOneOf.GetArrayLength());
        Assert.True(ContainsRequiredProperty(targetOneOf, "var"));
        Assert.True(ContainsRequiredProperty(targetOneOf, "globalObjectId"));
        Assert.True(ContainsRequiredProperties(targetOneOf, "scene", "hierarchyPath"));
        Assert.True(properties.TryGetProperty("depth", out var depthProperty));
        Assert.True(depthProperty.TryGetProperty("type", out var depthType));
        Assert.Equal(JsonValueKind.Array, depthType.ValueKind);
        Assert.True(ContainsArrayLiteral(depthType, "integer"));
        Assert.True(ContainsArrayLiteral(depthType, "null"));
        Assert.True(depthProperty.TryGetProperty("minimum", out var depthMinimum));
        Assert.Equal(0, depthMinimum.GetInt32());
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectRefresh)]
    [InlineData(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.ProjectSave)]
    public async Task Get_WhenOperationIsProjectMutation_ReturnsStrictEmptyObjectSchema (string operationName)
    {
        var catalog = new OperationCatalog(new InMemoryOperationCatalogProvider());

        var descriptor = await catalog.Get(operationName, CancellationToken.None);

        Assert.NotNull(descriptor);
        using var schemaDocument = JsonDocument.Parse(descriptor.ArgsSchemaJson);
        var schemaRoot = schemaDocument.RootElement;
        Assert.Equal(JsonValueKind.Object, schemaRoot.ValueKind);
        Assert.True(schemaRoot.TryGetProperty("type", out var typeProperty));
        Assert.Equal("object", typeProperty.GetString());
        Assert.True(schemaRoot.TryGetProperty("additionalProperties", out var additionalProperties));
        Assert.False(additionalProperties.GetBoolean());
        Assert.False(schemaRoot.TryGetProperty("properties", out _));
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
            new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen, UcliOperationKind.Query, OperationPolicy.Safe, ArgsSchemaJson),
            new UcliOperationDescriptor(MackySoft.Ucli.Contracts.Ipc.UcliPrimitiveOperationNames.SceneOpen, UcliOperationKind.Query, OperationPolicy.Safe, ArgsSchemaJson),
        ]);
        var catalog = new OperationCatalog(provider);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await TestAwaiter.WaitAsync(
                catalog.GetAll(CancellationToken.None).AsTask(),
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

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperations (
            ResolvedUnityProjectContext unityProject,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(unityProject);
            ArgumentNullException.ThrowIfNull(config);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(operations);
        }
    }

    private static bool ContainsRequiredProperty (
        JsonElement oneOfArray,
        string propertyName)
    {
        foreach (var schema in oneOfArray.EnumerateArray())
        {
            if (!schema.TryGetProperty("required", out var requiredProperties))
            {
                continue;
            }

            if (requiredProperties.ValueKind != JsonValueKind.Array || requiredProperties.GetArrayLength() != 1)
            {
                continue;
            }

            var requiredProperty = requiredProperties[0].GetString();
            if (string.Equals(requiredProperty, propertyName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsRequiredProperties (
        JsonElement oneOfArray,
        string firstPropertyName,
        string secondPropertyName)
    {
        foreach (var schema in oneOfArray.EnumerateArray())
        {
            if (!schema.TryGetProperty("required", out var requiredProperties))
            {
                continue;
            }

            if (requiredProperties.ValueKind != JsonValueKind.Array || requiredProperties.GetArrayLength() != 2)
            {
                continue;
            }

            var firstRequired = requiredProperties[0].GetString();
            var secondRequired = requiredProperties[1].GetString();
            if (string.Equals(firstRequired, firstPropertyName, StringComparison.Ordinal)
                && string.Equals(secondRequired, secondPropertyName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
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