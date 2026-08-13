
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

    /// <summary> Performs static validation and token issuance for a C# evaluation. </summary>
    [VocabularyText("eval.plan")]
    EvalPlan,

    /// <summary> Performs the single execution phase for a planned C# evaluation. </summary>
    [VocabularyText("eval.call")]
    EvalCall,

    /// <summary> Runs Unity tests. </summary>
    [VocabularyText("test.run")]
    TestRun,

    /// <summary> Persists and returns the start binding for one Lifecycle Execution. </summary>
    [VocabularyText("lifecycle.start")]
    LifecycleStart,

    /// <summary> Refreshes the Unity project through a durable Lifecycle Execution. </summary>
    [VocabularyText("project.refresh")]
    Refresh,

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

    /// <summary>Reads runtime-side GameView recording capability.</summary>
    [VocabularyText("recording.capability")]
    RecordingCapability,

    /// <summary>Starts or returns one durable GameView recording.</summary>
    [VocabularyText("recording.start")]
    RecordingStart,

    /// <summary>Reads one GameView recording or the current environment selection.</summary>
    [VocabularyText("recording.status")]
    RecordingStatus,

    /// <summary>Requests an idempotent stop for one GameView recording.</summary>
    [VocabularyText("recording.stop")]
    RecordingStop,

    /// <summary> Reads the fixed host and generation facts required to begin a Program Run. </summary>
    [VocabularyText("program.execution-context")]
    ProgramExecutionContext,

    /// <summary> Starts one Program-owned synchronous request by its logical execution identity. </summary>
    [VocabularyText("program.request.start")]
    ProgramRequestStart,

    /// <summary> Attaches to one Program-owned synchronous request without starting it. </summary>
    [VocabularyText("program.request.attach")]
    ProgramRequestAttach,

    /// <summary> Requests cancellation of a Program-owned Request without changing its logical identity. </summary>
    [VocabularyText("program.request.cancel")]
    ProgramRequestCancel,
}
