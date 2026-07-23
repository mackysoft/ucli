
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines BuildPipeline output layout shape literals. </summary>
[VocabularyDefinition]
public enum IpcBuildOutputLayoutShape
{
    /// <summary> BuildPipeline writes one file. </summary>
    [VocabularyText("file")]
    File = 1,

    /// <summary> BuildPipeline writes one directory. </summary>
    [VocabularyText("directory")]
    Directory = 2,

    /// <summary> BuildPipeline writes one macOS application bundle. </summary>
    [VocabularyText("appBundle")]
    AppBundle = 3,
}
