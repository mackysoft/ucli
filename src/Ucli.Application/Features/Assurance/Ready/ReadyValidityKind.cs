
namespace MackySoft.Ucli.Application.Features.Assurance.Ready;

/// <summary> Defines the finite validity scopes of ready claims. </summary>
[VocabularyDefinition]
internal enum ReadyValidityKind
{
    /// <summary> The claim remains valid for the observed reusable session. </summary>
    [VocabularyText("sessionBound")]
    SessionBound = 1,

    /// <summary> The claim is valid only for the completed probe. </summary>
    [VocabularyText("probeOnly")]
    ProbeOnly = 2,
}
