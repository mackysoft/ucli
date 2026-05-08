namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents optional Unity GUI bootstrap arguments supplied by the CLI. </summary>
/// <param name="OwnerProcessId"> The CLI owner process identifier. </param>
/// <param name="CanShutdownProcess"> Whether the launched GUI process may be shut down by uCLI. </param>
public sealed record IpcGuiBootstrapArguments (
    int OwnerProcessId,
    bool CanShutdownProcess);
