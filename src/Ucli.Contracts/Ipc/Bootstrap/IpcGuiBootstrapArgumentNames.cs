namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines Unity GUI bootstrap command-line argument names. </summary>
public static class IpcGuiBootstrapArgumentNames
{
    /// <summary> Gets the GUI bootstrap target argument name. </summary>
    public const string Target = "-ucliGuiBootstrapTarget";

    /// <summary> Gets the owner process identifier argument name. </summary>
    public const string OwnerProcessId = "-ucliGuiOwnerProcessId";

    /// <summary> Gets the process shutdown capability argument name. </summary>
    public const string CanShutdownProcess = "-ucliGuiCanShutdownProcess";
}
