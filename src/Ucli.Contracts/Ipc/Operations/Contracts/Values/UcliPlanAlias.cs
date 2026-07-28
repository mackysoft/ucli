using System.Text.Json.Serialization;
using MackySoft.JsonSchema.Generation.Annotations;
using MackySoft.Ucli.Contracts.Json;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Request-local alias produced by an earlier plan step. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[Length(1, int.MaxValue)]
public sealed class UcliPlanAlias : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UcliPlanAlias" /> class. </summary>
    /// <param name="value"> The request-local alias. </param>
    [JsonConstructor]
    public UcliPlanAlias (string value)
        : base(value)
    {
    }
}
