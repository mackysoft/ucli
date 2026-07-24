
namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Identifies the source category of a daemon primary diagnostic. </summary>
[VocabularyDefinition]
public enum DaemonDiagnosisPrimaryDiagnosticKind
{
    /// <summary> A C# compiler diagnostic. </summary>
    [VocabularyText("compiler")]
    Compiler = 1,

    /// <summary> A Unity package resolution diagnostic. </summary>
    [VocabularyText("packageResolution")]
    PackageResolution = 2,

    /// <summary> A uCLI plugin dependency diagnostic. </summary>
    [VocabularyText("pluginDependency")]
    PluginDependency = 3,

    /// <summary> A Unity GUI user-action diagnostic. </summary>
    [VocabularyText("unityDialog")]
    UnityDialog = 4,

    /// <summary> A Unity process exit diagnostic. </summary>
    [VocabularyText("processExit")]
    ProcessExit = 5,
}
