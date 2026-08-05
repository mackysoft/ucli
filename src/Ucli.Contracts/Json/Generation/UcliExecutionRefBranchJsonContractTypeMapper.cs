using MackySoft.JsonSchema.Generation.Extensibility;

namespace MackySoft.Ucli.Contracts.Json.Generation;

/// <summary>Maps a lifecycle-specific execution-reference converter to its tagged-union branch contract.</summary>
internal sealed class UcliExecutionRefBranchJsonContractTypeMapper : IJsonContractTypeMapper
{
    /// <inheritdoc />
    public string StableId => "ucli.execution-ref-branch";

    /// <inheritdoc />
    public string ContractVersion => "1";

    /// <inheritdoc />
    public bool CanMap (JsonContractTypeMapperContext context)
    {
        var effectiveConverter =
            context.PropertyInfo?.CustomConverter ?? context.TypeInfo.Converter;
        return GetContractType(context.TypeInfo.Type, effectiveConverter) is not null;
    }

    /// <inheritdoc />
    public JsonContractTypeMapping Map (JsonContractTypeMapperContext context)
    {
        var effectiveConverter =
            context.PropertyInfo?.CustomConverter ?? context.TypeInfo.Converter;
        return JsonContractTypeMapping.ContractType(
            GetContractType(context.TypeInfo.Type, effectiveConverter)
            ?? throw new InvalidOperationException(
                "The execution-reference branch mapper received an unsupported serializer contract."));
    }

    private static Type? GetContractType (
        Type valueType,
        object effectiveConverter)
    {
        if (valueType == typeof(ActiveExecutionRef)
            && effectiveConverter is ActiveExecutionRefBranchJsonConverter)
        {
            return typeof(IActiveExecutionRef);
        }
        if (valueType == typeof(RecoveryExecutionRef)
            && effectiveConverter is RecoveryExecutionRefBranchJsonConverter)
        {
            return typeof(IRecoveryExecutionRef);
        }
        if (valueType == typeof(TerminalExecutionRef)
            && effectiveConverter is TerminalExecutionRefBranchJsonConverter)
        {
            return typeof(ITerminalExecutionRef);
        }

        return null;
    }
}
