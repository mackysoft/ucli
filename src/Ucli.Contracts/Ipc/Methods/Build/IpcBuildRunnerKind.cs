using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines <c>build.run</c> runner-kind literals. </summary>
public enum IpcBuildRunnerKind
{
    /// <summary> Invokes Unity <c>BuildPipeline</c> through the uCLI Unity runtime. </summary>
    [UcliContractLiteral("buildPipeline")]
    BuildPipeline = 0,

    /// <summary> Invokes a Unity editor-side static method through the uCLI build runner bridge. </summary>
    [UcliContractLiteral("executeMethod")]
    ExecuteMethod = 1,
}
