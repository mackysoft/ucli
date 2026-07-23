
namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines whether a raw operation can be executed through Play Mode mutation requests. </summary>
[VocabularyDefinition]
public enum UcliOperationPlayModeSupport
{
    /// <summary> Disallows raw operation execution when <c>--allowPlayMode</c> is specified. </summary>
    [VocabularyText("disallowed")]
    Disallowed = 0,

    /// <summary> Allows raw operation execution both outside and inside Play Mode mutation requests. </summary>
    [VocabularyText("allowed")]
    Allowed = 1,

    /// <summary> Requires raw operation execution to use Play Mode mutation admission. </summary>
    [VocabularyText("required")]
    Required = 2,
}
