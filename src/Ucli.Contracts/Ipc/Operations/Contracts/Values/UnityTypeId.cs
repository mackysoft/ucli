using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Unity type identifier that must resolve in the project. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Unity type identifier that must resolve in the project.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
[UcliInputConstraint(UcliOperationInputConstraintKind.TypeExists)]
public sealed record UnityTypeId : UcliStringValue
{
    /// <summary> Initializes a new instance of the <see cref="UnityTypeId" /> class. </summary>
    /// <param name="value"> The Unity type identifier. </param>
    [JsonConstructor]
    public UnityTypeId (string value)
        : base(value)
    {
    }
}
