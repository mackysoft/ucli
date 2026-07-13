using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the methods supported by Unity IPC endpoints. </summary>
public enum UnityIpcMethod
{
    /// <summary> No Unity IPC method is specified. </summary>
    [UcliContractLiteralIgnore]
    Unspecified = 0,

    /// <summary> Checks connectivity to a Unity IPC endpoint. </summary>
    [UcliContractLiteral("ping")]
    Ping,

    /// <summary> Executes one Unity command request. </summary>
    [UcliContractLiteral("execute")]
    Execute,

    /// <summary> Runs Unity tests. </summary>
    [UcliContractLiteral("test.run")]
    TestRun,

    /// <summary> Assures Unity compilation. </summary>
    [UcliContractLiteral("compile")]
    Compile,

    /// <summary> Runs a Unity build assurance request. </summary>
    [UcliContractLiteral("build.run")]
    BuildRun,

    /// <summary> Reads the Unity operation catalog. </summary>
    [UcliContractLiteral("ops.read")]
    OpsRead,

    /// <summary> Reads a Unity asset-index snapshot. </summary>
    [UcliContractLiteral("index.assets.read")]
    IndexAssetsRead,

    /// <summary> Reads a Unity scene-tree-lite snapshot. </summary>
    [UcliContractLiteral("index.scene-tree-lite.read")]
    IndexSceneTreeLiteRead,

    /// <summary> Shuts down a Unity daemon endpoint. </summary>
    [UcliContractLiteral("shutdown")]
    Shutdown,

    /// <summary> Reads daemon log entries. </summary>
    [UcliContractLiteral("daemon.logs.read")]
    DaemonLogsRead,

    /// <summary> Reads Unity log entries. </summary>
    [UcliContractLiteral("unity.logs.read")]
    UnityLogsRead,

    /// <summary> Clears the Unity Editor Console. </summary>
    [UcliContractLiteral("unity.console.clear")]
    UnityConsoleClear,

    /// <summary> Captures one Unity Editor screenshot. </summary>
    [UcliContractLiteral("screenshot.capture")]
    ScreenshotCapture,

    /// <summary> Reads the current Unity Play Mode status. </summary>
    [UcliContractLiteral("play.status")]
    PlayStatus,

    /// <summary> Requests entry into Unity Play Mode. </summary>
    [UcliContractLiteral("play.enter")]
    PlayEnter,

    /// <summary> Requests exit from Unity Play Mode. </summary>
    [UcliContractLiteral("play.exit")]
    PlayExit,

    /// <summary> Rebootstraps a stopped GUI daemon endpoint. </summary>
    [UcliContractLiteral("gui.rebootstrap")]
    GuiRebootstrap,
}
