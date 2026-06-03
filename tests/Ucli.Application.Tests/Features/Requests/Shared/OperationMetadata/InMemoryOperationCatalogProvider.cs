using System.Buffers;
using System.Text;
using System.Text.Json;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class InMemoryOperationCatalogProvider : IOperationCatalogProvider
{
    private static readonly string ResolveArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(ResolveSelectorArgs));
    private static readonly string ScenePathArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(ScenePathArgs));
    private static readonly string PrefabPathArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(PrefabPathArgs));
    private static readonly string SceneTreeArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(SceneTreeArgs));
    private static readonly string GoCreateArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(GoCreateArgs));
    private static readonly string GoDescribeArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(GoDescribeArgs));
    private static readonly string SceneQueryArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(SceneQueryArgs));
    private static readonly string ComponentEnsureArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(ComponentEnsureArgs));
    private static readonly string ComponentSetArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(ComponentSetArgs));
    private static readonly string GoDeleteArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(GoTargetArgs));
    private static readonly string GoReparentArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(GoReparentArgs));
    private static readonly string AssetCreateArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(AssetCreateArgs));
    private static readonly string AssetSaveArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(AssetSaveArgs));
    private static readonly string AssetsFindArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(AssetsFindArgs));
    private static readonly string AssetSchemaArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(AssetSchemaArgs));
    private static readonly string CompSchemaArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(ComponentTypeArgs));
    private static readonly string AssetSetArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(AssetSetArgs));
    private static readonly string PrefabCreateArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(PrefabCreateArgs));
    private static readonly string StrictEmptyObjectArgsSchemaJson = CreatePublicArgsSchemaJson(typeof(UcliEmptyArgs));

    private static readonly IReadOnlyList<UcliOperationDescriptor> Operations =
    [
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.Resolve, UcliOperationKind.Query, OperationPolicy.Safe, ResolveArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.AssetCreate, UcliOperationKind.Mutation, OperationPolicy.Advanced, AssetCreateArgsSchemaJson, Exposure: UcliOperationExposure.EditLoweringOnly),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.AssetSave, UcliOperationKind.Mutation, OperationPolicy.Advanced, AssetSaveArgsSchemaJson, Exposure: UcliOperationExposure.EditLoweringOnly),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.AssetsFind, UcliOperationKind.Query, OperationPolicy.Safe, AssetsFindArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.AssetSchema, UcliOperationKind.Query, OperationPolicy.Safe, AssetSchemaArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.AssetSet, UcliOperationKind.Mutation, OperationPolicy.Advanced, AssetSetArgsSchemaJson, Exposure: UcliOperationExposure.EditLoweringOnly),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.CompEnsure, UcliOperationKind.Mutation, OperationPolicy.Advanced, ComponentEnsureArgsSchemaJson, Exposure: UcliOperationExposure.EditLoweringOnly),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.CompSchema, UcliOperationKind.Query, OperationPolicy.Safe, CompSchemaArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.CompSet, UcliOperationKind.Mutation, OperationPolicy.Advanced, ComponentSetArgsSchemaJson, Exposure: UcliOperationExposure.EditLoweringOnly),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.SceneOpen, UcliOperationKind.Command, OperationPolicy.Safe, ScenePathArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.SceneQuery, UcliOperationKind.Query, OperationPolicy.Safe, SceneQueryArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.SceneTree, UcliOperationKind.Query, OperationPolicy.Safe, SceneTreeArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.SceneSave, UcliOperationKind.Mutation, OperationPolicy.Advanced, ScenePathArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.GoCreate, UcliOperationKind.Mutation, OperationPolicy.Advanced, GoCreateArgsSchemaJson, Exposure: UcliOperationExposure.EditLoweringOnly),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.GoDelete, UcliOperationKind.Mutation, OperationPolicy.Advanced, GoDeleteArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.GoDescribe, UcliOperationKind.Query, OperationPolicy.Safe, GoDescribeArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.GoReparent, UcliOperationKind.Mutation, OperationPolicy.Advanced, GoReparentArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.PrefabCreate, UcliOperationKind.Mutation, OperationPolicy.Advanced, PrefabCreateArgsSchemaJson, Exposure: UcliOperationExposure.EditLoweringOnly),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.PrefabOpen, UcliOperationKind.Command, OperationPolicy.Safe, PrefabPathArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.PrefabSave, UcliOperationKind.Mutation, OperationPolicy.Advanced, PrefabPathArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.ProjectRefresh, UcliOperationKind.Command, OperationPolicy.Advanced, StrictEmptyObjectArgsSchemaJson),
        new UcliOperationDescriptor(UcliPrimitiveOperationNames.ProjectSave, UcliOperationKind.Mutation, OperationPolicy.Advanced, StrictEmptyObjectArgsSchemaJson),
    ];

    public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetOperationsAsync (CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(Operations);
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
        return ValueTask.FromResult(Operations);
    }

    private static string CreatePublicArgsSchemaJson (Type type)
    {
        using var document = JsonDocument.Parse(UcliOperationJsonSchemaGenerator.CreateArgsSchemaJson(type));
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            WritePublicSchema(document.RootElement, writer);
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    private static void WritePublicSchema (
        JsonElement element,
        Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    if (string.Equals(property.Name, UcliOperationContractPropertyNames.Alias, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    writer.WritePropertyName(property.Name);
                    WritePublicSchema(property.Value, writer);
                }

                writer.WriteEndObject();
                return;

            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String
                        && string.Equals(item.GetString(), UcliOperationContractPropertyNames.Alias, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    WritePublicSchema(item, writer);
                }

                writer.WriteEndArray();
                return;

            default:
                element.WriteTo(writer);
                return;
        }
    }
}
