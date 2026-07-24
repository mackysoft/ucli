using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

[UcliDescription("C# eval touched resources declaration.")]
public sealed record CsEvalTouchedResources
{
    [JsonConstructor]
    public CsEvalTouchedResources (
        CsEvalTouchedResourceState state,
        IReadOnlyList<CsEvalTouchedResourceDeclaration>? declared)
    {
        if (!TextVocabulary.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "C# eval touched-resource state must be specified.");
        }

        if (state == CsEvalTouchedResourceState.Declared)
        {
            if (declared == null || declared.Count == 0)
            {
                throw new ArgumentException("Declared touched-resource state requires at least one resource.", nameof(declared));
            }

            Declared = ContractArgumentGuard.RequireItems(declared, nameof(declared));
        }
        else
        {
            if (declared != null)
            {
                throw new ArgumentException("Touched resources must be omitted unless state is declared.", nameof(declared));
            }

            Declared = null;
        }

        State = state;
    }

    [UcliRequired]
    [UcliDescription("Touched-resource declaration completeness.")]
    public CsEvalTouchedResourceState State { get; }

    [UcliDescription("Declared touched resources when state is declared.")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CsEvalTouchedResourceDeclaration>? Declared { get; }
}
