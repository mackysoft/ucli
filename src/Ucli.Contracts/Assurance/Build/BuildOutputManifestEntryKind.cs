
namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Defines output source entry kind literals in <c>output-manifest.json</c>. </summary>
[VocabularyDefinition]
internal enum BuildOutputManifestEntryKind
{
    /// <summary> The source entry was a regular file. </summary>
    [VocabularyText("file")]
    File = 1,

    /// <summary> The source entry was a directory. </summary>
    [VocabularyText("directory")]
    Directory = 2,
}
