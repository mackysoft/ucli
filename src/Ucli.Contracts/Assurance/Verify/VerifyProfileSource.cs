
namespace MackySoft.Ucli.Contracts.Assurance;

/// <summary> Identifies how an effective verify profile was supplied. </summary>
[VocabularyDefinition]
public enum VerifyProfileSource
{
    /// <summary> The profile is built into uCLI. </summary>
    [VocabularyText("builtIn")]
    BuiltIn = 1,

    /// <summary> The profile was loaded from a repository file. </summary>
    [VocabularyText("file")]
    File = 2,
}
