using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval operation arguments.")]
public sealed record CsEvalArgs
{
    [JsonConstructor]
    public CsEvalArgs (
        string source)
    {
        Source = source;
    }

    [UcliRequired]
    [UcliDescription("C# source to compile in memory. Accepts either a complete compilation unit or a Run method body snippet.")]
    [UcliInputConstraint(UcliOperationInputConstraintKind.NonEmpty)]
    public string Source { get; init; }
}
