
namespace MackySoft.Ucli.Contracts.Operations;

/// <summary> Defines supported reference target-kind constraint parameters. </summary>
[VocabularyDefinition]
public enum UcliOperationReferenceTargetKind
{
    /// <summary> Asset reference target. </summary>
    [VocabularyText("asset")]
    Asset = 1,

    /// <summary> Component reference target. </summary>
    [VocabularyText("component")]
    Component = 2,

    /// <summary> GameObject reference target. </summary>
    [VocabularyText("gameObject")]
    GameObject = 3,
}
