using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Operations;

internal static class UcliOperationContractValidatorTestContracts
{
    internal sealed record ReferenceArgs (
        GameObjectReferenceArgs? Target);

    internal sealed record CamelCaseReservedVarArgs (string? Var);

    internal sealed record NestedReservedVarArgs (NestedReservedVarValue Value);

    internal sealed record NestedReservedVarValue (string? Var);

    internal sealed record RenamedReservedVarArgs (
        [property: JsonPropertyName("value")]
        string? Var);

    internal sealed record IgnoredReservedVarArgs (
        [property: JsonIgnore]
        string? Var);

    internal sealed record ConvertedNestedArgs (ConvertedScalarValue Value);

    [JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
    internal sealed class ConvertedScalarValue : UcliStringValue
    {
        public ConvertedScalarValue (string value)
            : base(value)
        {
        }

        public string? Var => "not serialized";
    }

    [JsonPolymorphic(TypeDiscriminatorPropertyName = UcliOperationContractPropertyNames.Alias)]
    [JsonDerivedType(typeof(ReservedDiscriminatorFirstArgs), 1)]
    [JsonDerivedType(typeof(ReservedDiscriminatorSecondArgs), 2)]
    internal abstract record ReservedDiscriminatorArgs;

    internal sealed record ReservedDiscriminatorFirstArgs (string? Value)
        : ReservedDiscriminatorArgs;

    internal sealed record ReservedDiscriminatorSecondArgs (int Value)
        : ReservedDiscriminatorArgs;
}
