using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Assurance.Build;

/// <summary> Defines output source entry kind literals in <c>output-manifest.json</c>. </summary>
internal enum BuildOutputManifestEntryKind
{
    /// <summary> The source entry was a regular file. </summary>
    [UcliContractLiteral("file")]
    File = 0,

    /// <summary> The source entry was a directory. </summary>
    [UcliContractLiteral("directory")]
    Directory = 1,
}
