using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Tests.Ipc.Common;

public sealed class IpcProtocolVocabularyTests
{
    public static TheoryData<UnityIpcMethod, string> UnityIpcMethodCases => new()
    {
        { UnityIpcMethod.Ping, "ping" },
        { UnityIpcMethod.Execute, "execute" },
        { UnityIpcMethod.TestRun, "test.run" },
        { UnityIpcMethod.Compile, "compile" },
        { UnityIpcMethod.BuildRun, "build.run" },
        { UnityIpcMethod.OpsRead, "ops.read" },
        { UnityIpcMethod.IndexAssetsRead, "index.assets.read" },
        { UnityIpcMethod.IndexSceneTreeLiteRead, "index.scene-tree-lite.read" },
        { UnityIpcMethod.Shutdown, "shutdown" },
        { UnityIpcMethod.DaemonLogsRead, "daemon.logs.read" },
        { UnityIpcMethod.UnityLogsRead, "unity.logs.read" },
        { UnityIpcMethod.UnityConsoleClear, "unity.console.clear" },
        { UnityIpcMethod.ScreenshotCapture, "screenshot.capture" },
        { UnityIpcMethod.PlayStatus, "play.status" },
        { UnityIpcMethod.PlayEnter, "play.enter" },
        { UnityIpcMethod.PlayExit, "play.exit" },
        { UnityIpcMethod.GuiRebootstrap, "gui.rebootstrap" },
    };

    public static TheoryData<string?> InvalidUnityIpcMethodLiterals => new()
    {
        null,
        "",
        " ",
        "unknown",
        "PING",
        " ping",
        "ping ",
    };

    [Fact]
    [Trait("Size", "Small")]
    public void IpcProtocol_ExposesStableLiterals ()
    {
        Assert.Equal(1, IpcProtocol.CurrentVersion);
        Assert.Equal("ok", ContractLiteralCodec.ToValue(IpcResponseStatus.Ok));
        Assert.Equal("error", ContractLiteralCodec.ToValue(IpcResponseStatus.Error));
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(UnityIpcMethodCases))]
    public void UnityIpcMethod_WhenMapped_RoundTripsCanonicalLiteral (
        UnityIpcMethod method,
        string expectedLiteral)
    {
        var result = ContractLiteralCodec.TryParse(expectedLiteral, out UnityIpcMethod parsedMethod);

        Assert.True(result);
        Assert.Equal(method, parsedMethod);
        Assert.Equal(expectedLiteral, ContractLiteralCodec.ToValue(method));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityIpcMethod_WhenValueIsZero_IsNotMapped ()
    {
        const UnityIpcMethod method = (UnityIpcMethod)0;

        Assert.False(ContractLiteralCodec.IsDefined(method));
        Assert.False(ContractLiteralCodec.TryToValue(method, out var literal));
        Assert.Null(literal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityIpcMethod_WhenValueIsUndefined_IsNotMapped ()
    {
        var method = (UnityIpcMethod)999;

        Assert.False(ContractLiteralCodec.IsDefined(method));
        Assert.False(ContractLiteralCodec.TryToValue(method, out var literal));
        Assert.Null(literal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidUnityIpcMethodLiterals))]
    public void UnityIpcMethod_WhenLiteralIsNotCanonical_IsRejected (string? literal)
    {
        var result = ContractLiteralCodec.TryParse(literal, out UnityIpcMethod method);

        Assert.False(result);
        Assert.Equal(default, method);
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
        Assert.Equal(["enter", "exit"], ContractLiteralCodec.GetLiterals<IpcPlayTransitionCommand>());
        Assert.Equal(
            ["entered", "alreadyEntered", "exited", "alreadyExited", "timeout", "blocked"],
            ContractLiteralCodec.GetLiterals<IpcPlayTransitionOutcome>());
        Assert.Equal(
            ["notApplied", "applied", "indeterminate", "unknown"],
            ContractLiteralCodec.GetLiterals<IpcApplicationState>());
        Assert.False(ContractLiteralCodec.IsDefined((IpcPlayTransitionCommand)0));
        Assert.False(ContractLiteralCodec.IsDefined((IpcPlayTransitionOutcome)0));
        Assert.False(ContractLiteralCodec.IsDefined((IpcApplicationState)0));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteLiteralEnums_ExposeExpectedLiterals ()
    {
        Assert.Equal(["info", "warning", "error"], ContractLiteralCodec.GetLiterals<UcliDiagnosticSeverity>());
        Assert.Equal(["none", "partial", "indeterminate"], ContractLiteralCodec.GetLiterals<IpcExecuteDiagnosticCoverageImpact>());
        Assert.Equal(["validate", "plan", "call", "skipped"], ContractLiteralCodec.GetLiterals<IpcExecuteOperationPhase>());
        Assert.Equal(["assetSearch", "guidPath", "sceneTreeLite"], ContractLiteralCodec.GetLiterals<IpcExecuteReadPostconditionSurface>());
        Assert.Equal(["edit", "operation", "refresh"], ContractLiteralCodec.GetLiterals<IpcExecutePostReadSourceKind>());
        Assert.Equal(["none", "context", "project"], ContractLiteralCodec.GetLiterals<IpcExecutePostReadCommit>());
        Assert.Equal(["deterministic", "unavailable"], ContractLiteralCodec.GetLiterals<IpcExecuteExpectedPostState>());

        Assert.False(ContractLiteralCodec.IsDefined((UcliDiagnosticSeverity)0));
        Assert.False(ContractLiteralCodec.IsDefined((IpcExecuteDiagnosticCoverageImpact)0));
        Assert.False(ContractLiteralCodec.IsDefined((IpcExecuteOperationPhase)0));
        Assert.False(ContractLiteralCodec.IsDefined((IpcExecuteReadPostconditionSurface)0));
        Assert.False(ContractLiteralCodec.IsDefined((IpcExecutePostReadSourceKind)0));
        Assert.False(ContractLiteralCodec.IsDefined((IpcExecutePostReadCommit)0));
        Assert.False(ContractLiteralCodec.IsDefined((IpcExecuteExpectedPostState)0));
    }
}
