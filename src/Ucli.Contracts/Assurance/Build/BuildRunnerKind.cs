
namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Defines the supported build runner kinds. </summary>
[VocabularyDefinition]
public enum BuildRunnerKind
{
    /// <summary> Invokes Unity <c>BuildPipeline</c> through the uCLI Unity runtime. </summary>
    [VocabularyText("buildPipeline")]
    BuildPipeline = 1,

    /// <summary> Invokes a Unity editor-side static method through the uCLI build runner bridge. </summary>
    [VocabularyText("executeMethod")]
    ExecuteMethod = 2,
}
