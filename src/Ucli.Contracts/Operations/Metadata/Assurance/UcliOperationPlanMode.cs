
namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported operation plan modes. </summary>
[VocabularyDefinition]
public enum UcliOperationPlanMode
{
    /// <summary> Plans that only validate args and static preconditions. </summary>
    [VocabularyText("validationOnly")]
    ValidationOnly = 0,

    /// <summary> Plans that observe live Unity state without creating preview state. </summary>
    [VocabularyText("observesLiveUnity")]
    ObservesLiveUnity = 1,

    /// <summary> Plans that can create request-scoped preview state. </summary>
    [VocabularyText("mayCreatePreviewState")]
    MayCreatePreviewState = 2,
}
