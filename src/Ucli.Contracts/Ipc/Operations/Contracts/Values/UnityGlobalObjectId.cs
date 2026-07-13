using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Unity GlobalObjectId string used for exact object resolution. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Unity GlobalObjectId string used for exact object resolution.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.GlobalObjectId)]
public sealed record UnityGlobalObjectId : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityGlobalObjectId" /> class. </summary>
    /// <param name="value"> The Unity GlobalObjectId string. </param>
    [JsonConstructor]
    public UnityGlobalObjectId (string value)
        : base(value)
    {
    }
}
