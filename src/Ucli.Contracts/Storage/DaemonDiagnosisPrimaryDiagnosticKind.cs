using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Storage;

/// <summary> Identifies the source category of a daemon primary diagnostic. </summary>
public enum DaemonDiagnosisPrimaryDiagnosticKind
{
    /// <summary> A C# compiler diagnostic. </summary>
    [UcliContractLiteral("compiler")]
    Compiler = 1,

    /// <summary> A Unity package resolution diagnostic. </summary>
    [UcliContractLiteral("packageResolution")]
    PackageResolution = 2,

    /// <summary> A uCLI plugin dependency diagnostic. </summary>
    [UcliContractLiteral("pluginDependency")]
    PluginDependency = 3,

    /// <summary> A Unity GUI user-action diagnostic. </summary>
    [UcliContractLiteral("unityDialog")]
    UnityDialog = 4,

    /// <summary> A Unity process exit diagnostic. </summary>
    [UcliContractLiteral("processExit")]
    ProcessExit = 5,
}
