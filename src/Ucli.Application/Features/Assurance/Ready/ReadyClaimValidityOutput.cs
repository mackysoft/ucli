using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Represents validity semantics for one ready claim. </summary>
internal sealed record ReadyClaimValidityOutput
{
    /// <summary> Initializes validity semantics with a defined scope. </summary>
    /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="Kind" /> is not defined by the ready contract. </exception>
    [JsonConstructor]
    public ReadyClaimValidityOutput (
        ReadyValidityKind Kind,
        bool GuaranteesReusableSession)
    {
        if (!TextVocabulary.IsDefined(Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(Kind), Kind, "Validity kind must be defined by the ready contract.");
        }

        this.Kind = Kind;
        this.GuaranteesReusableSession = GuaranteesReusableSession;
    }

    /// <summary> Gets the validity scope of the ready claim. </summary>
    public ReadyValidityKind Kind { get; }

    /// <summary> Gets whether the claim guarantees a reusable Unity session. </summary>
    public bool GuaranteesReusableSession { get; }
}
