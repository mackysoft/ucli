using System.Text;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Tests;

internal sealed class InMemoryOperationCatalogProvider : IOperationCatalogProvider
{
    private static readonly IReadOnlyList<UcliOperationDescriptor> Operations =
    [
        CreateDescriptor<ResolveSelectorArgs>(UcliPrimitiveOperationNames.Resolve, UcliOperationKind.Query, OperationPolicy.Safe),
        CreateDescriptor<AssetCreateArgs>(UcliPrimitiveOperationNames.AssetCreate, UcliOperationKind.Mutation, OperationPolicy.Advanced, UcliOperationExposure.EditLoweringOnly),
        CreateDescriptor<AssetSaveArgs>(UcliPrimitiveOperationNames.AssetSave, UcliOperationKind.Mutation, OperationPolicy.Advanced, UcliOperationExposure.EditLoweringOnly),
        CreateDescriptor<AssetsFindArgs>(UcliPrimitiveOperationNames.AssetsFind, UcliOperationKind.Query, OperationPolicy.Safe),
        CreateDescriptor<AssetSchemaArgs>(UcliPrimitiveOperationNames.AssetSchema, UcliOperationKind.Query, OperationPolicy.Safe),
        CreateDescriptor<AssetSetArgs>(UcliPrimitiveOperationNames.AssetSet, UcliOperationKind.Mutation, OperationPolicy.Advanced, UcliOperationExposure.EditLoweringOnly),
        CreateDescriptor<ComponentEnsureArgs>(UcliPrimitiveOperationNames.CompEnsure, UcliOperationKind.Mutation, OperationPolicy.Advanced, UcliOperationExposure.EditLoweringOnly),
        CreateDescriptor<ComponentTypeArgs>(UcliPrimitiveOperationNames.CompSchema, UcliOperationKind.Query, OperationPolicy.Safe),
        CreateDescriptor<ComponentSetArgs>(UcliPrimitiveOperationNames.CompSet, UcliOperationKind.Mutation, OperationPolicy.Advanced, UcliOperationExposure.EditLoweringOnly),
        CreateDescriptor<ScenePathArgs>(UcliPrimitiveOperationNames.SceneOpen, UcliOperationKind.Command, OperationPolicy.Safe),
        CreateDescriptor<SceneQueryArgs>(UcliPrimitiveOperationNames.SceneQuery, UcliOperationKind.Query, OperationPolicy.Safe),
        CreateDescriptor<SceneTreeArgs>(UcliPrimitiveOperationNames.SceneTree, UcliOperationKind.Query, OperationPolicy.Safe),
        CreateDescriptor<ScenePathArgs>(UcliPrimitiveOperationNames.SceneSave, UcliOperationKind.Mutation, OperationPolicy.Advanced),
        CreateDescriptor<GoCreateArgs>(UcliPrimitiveOperationNames.GoCreate, UcliOperationKind.Mutation, OperationPolicy.Advanced, UcliOperationExposure.EditLoweringOnly),
        CreateDescriptor<GoTargetArgs>(UcliPrimitiveOperationNames.GoDelete, UcliOperationKind.Mutation, OperationPolicy.Advanced),
        CreateDescriptor<GoDescribeArgs>(UcliPrimitiveOperationNames.GoDescribe, UcliOperationKind.Query, OperationPolicy.Safe),
        CreateDescriptor<GoReparentArgs>(UcliPrimitiveOperationNames.GoReparent, UcliOperationKind.Mutation, OperationPolicy.Advanced),
        CreateDescriptor<PrefabCreateArgs>(UcliPrimitiveOperationNames.PrefabCreate, UcliOperationKind.Mutation, OperationPolicy.Advanced, UcliOperationExposure.EditLoweringOnly),
        CreateDescriptor<PrefabPathArgs>(UcliPrimitiveOperationNames.PrefabOpen, UcliOperationKind.Command, OperationPolicy.Safe),
        CreateDescriptor<PrefabPathArgs>(UcliPrimitiveOperationNames.PrefabSave, UcliOperationKind.Mutation, OperationPolicy.Advanced),
        CreateDescriptor<UcliEmptyArgs>(UcliPrimitiveOperationNames.ProjectSave, UcliOperationKind.Mutation, OperationPolicy.Advanced),
    ];

    private readonly IReadOnlyList<UcliOperationDescriptor> operations;

    public InMemoryOperationCatalogProvider ()
        : this(Operations)
    {
    }

    public InMemoryOperationCatalogProvider (IReadOnlyList<UcliOperationDescriptor> operations)
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

    private static UcliOperationDescriptor CreateDescriptor<TArgs> (
        string operationName,
        UcliOperationKind kind,
        OperationPolicy policy,
        UcliOperationExposure exposure = UcliOperationExposure.Public)
    {
        var generation = UcliOperationJsonContractGenerator.Generate(
            operationName,
            IpcJsonSerializerOptions.PublicRawOperationContracts.GetTypeInfo(typeof(TArgs)),
            resultTypeInfo: null);
        return new UcliOperationDescriptor(
            operationName,
            kind,
            policy,
            Encoding.UTF8.GetString(generation.GetArgsJsonSchemaUtf8()),
            DescriptorDigest: Sha256DigestTestFactory.Compute(operationName),
            VerdictContract: null,
            ResultSchemaJson: null,
            Exposure: exposure);
    }
}
