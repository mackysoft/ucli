
namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Defines the methods supported by Unity IPC endpoints. </summary>
[VocabularyDefinition]
public enum UnityIpcMethod
{
    /// <summary> Checks connectivity to a Unity IPC endpoint. </summary>
    [VocabularyText("ping")]
    Ping = 1,

    /// <summary> Executes one Unity command request. </summary>
    [VocabularyText("execute")]
    Execute,

    /// <summary> Runs Unity tests. </summary>
    [VocabularyText("test.run")]
    TestRun,

    /// <summary> Assures Unity compilation. </summary>
    [VocabularyText("compile")]
    Compile,

    /// <summary> Runs a Unity build assurance request. </summary>
    [VocabularyText("build.run")]
    BuildRun,

    /// <summary> Reads the Unity operation catalog. </summary>
    [VocabularyText("ops.read")]
    OpsRead,

    /// <summary> Reads a Unity asset-index snapshot. </summary>
    [VocabularyText("index.assets.read")]
    IndexAssetsRead,

    /// <summary> Reads a Unity scene-tree-lite snapshot. </summary>
    [VocabularyText("index.scene-tree-lite.read")]
    IndexSceneTreeLiteRead,

    /// <summary> Shuts down a Unity daemon endpoint. </summary>
    [VocabularyText("shutdown")]
    Shutdown,

    /// <summary> Reads daemon log entries. </summary>
    [VocabularyText("daemon.logs.read")]
    DaemonLogsRead,

    /// <summary> Reads Unity log entries. </summary>
    [VocabularyText("unity.logs.read")]
    UnityLogsRead,

    /// <summary> Clears the Unity Editor Console. </summary>
    [VocabularyText("unity.console.clear")]
    UnityConsoleClear,

    /// <summary> Captures one Unity Editor screenshot. </summary>
    [VocabularyText("screenshot.capture")]
    ScreenshotCapture,

    /// <summary> Reads the current Unity Play Mode status. </summary>
    [VocabularyText("play.status")]
    PlayStatus,

    /// <summary> Requests entry into Unity Play Mode. </summary>
    [VocabularyText("play.enter")]
    PlayEnter,

    /// <summary> Requests exit from Unity Play Mode. </summary>
    [VocabularyText("play.exit")]
    PlayExit,

    /// <summary> Rebootstraps a stopped GUI daemon endpoint. </summary>
    [VocabularyText("gui.rebootstrap")]
    GuiRebootstrap,
}
