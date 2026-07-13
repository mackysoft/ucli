using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines script-compilation states exposed by lifecycle-bearing IPC contracts. </summary>
public enum IpcCompileState
{
    /// <summary> Script compilation is inactive and no compile failure is reported. </summary>
    [UcliContractLiteral("ready")]
    Ready = 0,

    /// <summary> Script compilation is active. </summary>
    [UcliContractLiteral("compiling")]
    Compiling = 1,

    /// <summary> The latest completed script compilation failed. </summary>
    [UcliContractLiteral("failed")]
    Failed = 2,
}
