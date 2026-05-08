namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines command-line argument names used to bootstrap Unity batchmode execution. </summary>
public static class IpcBatchmodeBootstrapArgumentNames
{
    /// <summary> Gets the argument name that carries the batchmode bootstrap target literal. </summary>
    public const string Target = "-ucliBootstrapTarget";

    /// <summary> Gets the argument name that carries the Unity project fingerprint value. </summary>
    public const string ProjectFingerprint = "-ucliProjectFingerprint";
}
