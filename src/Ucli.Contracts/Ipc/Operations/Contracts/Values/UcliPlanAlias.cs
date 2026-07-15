using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Request-local alias produced by an earlier plan step. </summary>
[JsonConverter(typeof(UcliStringValueJsonConverterFactory))]
[UcliDescription("Request-local alias produced by an earlier plan step.")]
[UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
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
