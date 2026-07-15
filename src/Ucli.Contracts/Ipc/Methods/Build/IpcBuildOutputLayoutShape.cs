using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines BuildPipeline output layout shape literals. </summary>
public enum IpcBuildOutputLayoutShape
{
    /// <summary> BuildPipeline writes one file. </summary>
    [UcliContractLiteral("file")]
    File = 1,

    /// <summary> BuildPipeline writes one directory. </summary>
    [UcliContractLiteral("directory")]
    Directory = 2,

    /// <summary> BuildPipeline writes one macOS application bundle. </summary>
    [UcliContractLiteral("appBundle")]
    AppBundle = 3,
}
