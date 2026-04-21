namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines supported IPC method names. </summary>
public static class IpcMethodNames
{
    /// <summary> Gets the method name used for connectivity checks. </summary>
    public const string Ping = "ping";

    /// <summary> Gets the method name used for Unity command execution requests. </summary>
    public const string Execute = "execute";

    /// <summary> Gets the method name used for Unity test-run requests. </summary>
    public const string TestRun = "test.run";

    /// <summary> Gets the method name used for Unity ops-catalog read requests. </summary>
    public const string OpsRead = "ops.read";

    /// <summary> Gets the method name used for Unity asset-index snapshot read requests. </summary>
    public const string IndexAssetsRead = "index.assets.read";

    /// <summary> Gets the method name used for Unity scene-tree-lite snapshot read requests. </summary>
    public const string IndexSceneTreeLiteRead = "index.scene-tree-lite.read";

    /// <summary> Gets the method name used for daemon shutdown requests. </summary>
    public const string Shutdown = "shutdown";

    /// <summary> Gets the method name used for daemon log stream read requests. </summary>
    public const string DaemonLogsRead = "daemon.logs.read";

    /// <summary> Gets the method name used for Unity log stream read requests. </summary>
    public const string UnityLogsRead = "unity.logs.read";
}