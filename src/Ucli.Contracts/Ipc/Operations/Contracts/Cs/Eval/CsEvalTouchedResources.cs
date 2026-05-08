using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval touched resources declaration.")]
public sealed record CsEvalTouchedResources
{
    [JsonConstructor]
    public CsEvalTouchedResources (
        string state,
        IReadOnlyList<CsEvalTouchedResourceDeclaration>? declared)
    {
        State = state;
        Declared = declared;
    }

    [UcliRequired]
    [UcliDescription("Touched resource state literal: unknown, none, or declared.")]
    public string State { get; init; }

    [UcliDescription("Declared touched resources when state is declared.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CsEvalTouchedResourceDeclaration>? Declared { get; init; }
}
