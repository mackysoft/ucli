using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval operation arguments.")]
public sealed record CsEvalArgs
{
    [JsonConstructor]
    public CsEvalArgs (
        string source)
    {
        Source = ContractArgumentGuard.RequireValue(source, nameof(source));
    }

    [UcliRequired]
    [UcliDescription("C# source to compile in memory. Accepts either a complete compilation unit or a Run method body snippet.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
    public string Source { get; }
}
