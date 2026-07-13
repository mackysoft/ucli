using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcProtocolVocabularyTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void IpcProtocol_ExposesStableLiterals ()
    {
        Assert.Equal(1, IpcProtocol.CurrentVersion);
        Assert.Equal("ok", IpcProtocol.StatusOk);
        Assert.Equal("error", IpcProtocol.StatusError);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcMethodNames_ExposeExpectedMethodLiterals ()
    {
        Assert.Equal("ping", IpcMethodNames.Ping);
        Assert.Equal("execute", IpcMethodNames.Execute);
        Assert.Equal("ops.read", IpcMethodNames.OpsRead);
        Assert.Equal("index.assets.read", IpcMethodNames.IndexAssetsRead);
        Assert.Equal("index.scene-tree-lite.read", IpcMethodNames.IndexSceneTreeLiteRead);
        Assert.Equal("test.run", IpcMethodNames.TestRun);
        Assert.Equal("compile", IpcMethodNames.Compile);
        Assert.Equal("shutdown", IpcMethodNames.Shutdown);
        Assert.Equal("daemon.logs.read", IpcMethodNames.DaemonLogsRead);
        Assert.Equal("unity.logs.read", IpcMethodNames.UnityLogsRead);
        Assert.Equal("unity.console.clear", IpcMethodNames.UnityConsoleClear);
        Assert.Equal("screenshot.capture", IpcMethodNames.ScreenshotCapture);
        Assert.Equal("play.status", IpcMethodNames.PlayStatus);
        Assert.Equal("play.enter", IpcMethodNames.PlayEnter);
        Assert.Equal("play.exit", IpcMethodNames.PlayExit);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcLifecycleLiteralContracts_ExposeExpectedLiterals ()
    {
        Assert.Equal(
            [
                "starting",
                "recovering",
                "ready",
                "busy",
                "compiling",
                "compileFailed",
                "domainReloading",
                "reimporting",
                "playmode",
                "modalBlocked",
                "safeMode",
                "shuttingDown",
                "unavailable",
            ],
            ContractLiteralCodec.GetLiterals<IpcEditorLifecycleState>());
        Assert.Equal(
            ["ready", "compiling", "failed"],
            ContractLiteralCodec.GetLiterals<IpcCompileState>());
        Assert.Equal(
            [
                "startup",
                "busy",
                "recovery",
                "compile",
                "compileFailed",
                "domainReload",
                "reimport",
                "playMode",
                "modalDialog",
                "safeMode",
                "shutdown",
                "unavailable",
            ],
            ContractLiteralCodec.GetLiterals<IpcEditorBlockingReason>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcScreenshotLiteralContracts_ExposeExpectedLiterals ()
    {
        Assert.Equal(["game", "scene"], ContractLiteralCodec.GetLiterals<IpcScreenshotTarget>());
        Assert.Equal(
            ["currentSurface", "requestedResolution"],
            ContractLiteralCodec.GetLiterals<IpcScreenshotSizeMode>());
        Assert.Equal(["gamma", "linear"], ContractLiteralCodec.GetLiterals<IpcScreenshotColorSpace>());
        Assert.Equal(["rgba8Srgb"], ContractLiteralCodec.GetLiterals<IpcScreenshotPixelFormat>());
        Assert.Equal(["topDown"], ContractLiteralCodec.GetLiterals<IpcScreenshotRowOrder>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcPlayLiteralContracts_ExposeExpectedLiterals ()
    {
        Assert.Equal(0, (int)IpcPlayModeState.Stopped);
        Assert.Equal(1, (int)IpcPlayModeState.Entering);
        Assert.Equal(2, (int)IpcPlayModeState.Playing);
        Assert.Equal(3, (int)IpcPlayModeState.Exiting);
        Assert.Equal(4, (int)IpcPlayModeState.Unknown);
        Assert.Equal(0, (int)IpcPlayModeTransition.None);
        Assert.Equal(1, (int)IpcPlayModeTransition.Entering);
        Assert.Equal(2, (int)IpcPlayModeTransition.Exiting);
        Assert.Equal("enter", IpcPlayTransitionCommandNames.Enter);
        Assert.Equal("exit", IpcPlayTransitionCommandNames.Exit);
        Assert.Equal("entered", IpcPlayTransitionResultNames.Entered);
        Assert.Equal("alreadyEntered", IpcPlayTransitionResultNames.AlreadyEntered);
        Assert.Equal("exited", IpcPlayTransitionResultNames.Exited);
        Assert.Equal("alreadyExited", IpcPlayTransitionResultNames.AlreadyExited);
        Assert.Equal("timeout", IpcPlayTransitionResultNames.Timeout);
        Assert.Equal("blocked", IpcPlayTransitionResultNames.Blocked);
        Assert.Equal("notApplied", IpcPlayApplicationStateNames.NotApplied);
        Assert.Equal("applied", IpcPlayApplicationStateNames.Applied);
        Assert.Equal("indeterminate", IpcPlayApplicationStateNames.Indeterminate);
        Assert.Equal("unknown", IpcPlayApplicationStateNames.Unknown);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteReadPostconditionSurfaceNames_ExposeExpectedLiterals ()
    {
        Assert.Equal("assetSearch", IpcExecuteReadPostconditionSurfaceNames.AssetSearch);
        Assert.Equal("guidPath", IpcExecuteReadPostconditionSurfaceNames.GuidPath);
        Assert.Equal("sceneTreeLite", IpcExecuteReadPostconditionSurfaceNames.SceneTreeLite);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteApplicationStateNames_ExposeExpectedLiterals ()
    {
        Assert.Equal("notApplied", IpcExecuteApplicationStateNames.NotApplied);
        Assert.Equal("applied", IpcExecuteApplicationStateNames.Applied);
        Assert.Equal("indeterminate", IpcExecuteApplicationStateNames.Indeterminate);
        Assert.Equal("unknown", IpcExecuteApplicationStateNames.Unknown);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteOperationPhaseNames_ExposeExpectedLiterals ()
    {
        Assert.Equal("validate", IpcExecuteOperationPhaseNames.Validate);
        Assert.Equal("plan", IpcExecuteOperationPhaseNames.Plan);
        Assert.Equal("call", IpcExecuteOperationPhaseNames.Call);
        Assert.Equal("skipped", IpcExecuteOperationPhaseNames.Skipped);
    }
}
