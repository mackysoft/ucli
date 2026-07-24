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
        Assert.Equal("ok", TextVocabulary.GetText(IpcResponseStatus.Ok));
        Assert.Equal("error", TextVocabulary.GetText(IpcResponseStatus.Error));
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(UnityIpcMethodCases))]
    public void UnityIpcMethod_WhenMapped_RoundTripsCanonicalLiteral (
        UnityIpcMethod method,
        string expectedLiteral)
    {
        var result = TextVocabulary.TryGetValue(expectedLiteral, out UnityIpcMethod parsedMethod);

        Assert.True(result);
        Assert.Equal(method, parsedMethod);
        Assert.Equal(expectedLiteral, TextVocabulary.GetText(method));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityIpcMethod_WhenValueIsZero_IsNotMapped ()
    {
        const UnityIpcMethod method = (UnityIpcMethod)0;

        Assert.False(TextVocabulary.IsDefined(method));
        Assert.False(TextVocabulary.TryGetText(method, out var literal));
        Assert.Null(literal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UnityIpcMethod_WhenValueIsUndefined_IsNotMapped ()
    {
        var method = (UnityIpcMethod)999;

        Assert.False(TextVocabulary.IsDefined(method));
        Assert.False(TextVocabulary.TryGetText(method, out var literal));
        Assert.Null(literal);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidUnityIpcMethodLiterals))]
    public void UnityIpcMethod_WhenLiteralIsNotCanonical_IsRejected (string? literal)
    {
        var result = TextVocabulary.TryGetValue(literal, out UnityIpcMethod method);

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
            TextVocabulary.GetTexts<IpcEditorLifecycleState>());
        Assert.Equal(
            ["ready", "compiling", "failed"],
            TextVocabulary.GetTexts<IpcCompileState>());
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
            TextVocabulary.GetTexts<IpcEditorBlockingReason>());
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcScreenshotLiteralContracts_ExposeExpectedLiterals ()
    {
        Assert.Equal(["game", "scene"], TextVocabulary.GetTexts<IpcScreenshotTarget>());
        Assert.Equal(
            ["currentSurface", "requestedResolution"],
            TextVocabulary.GetTexts<IpcScreenshotSizeMode>());
        Assert.Equal(["gamma", "linear"], TextVocabulary.GetTexts<IpcScreenshotColorSpace>());
        Assert.Equal(["rgba8Srgb"], TextVocabulary.GetTexts<IpcScreenshotPixelFormat>());
        Assert.Equal(["topDown"], TextVocabulary.GetTexts<IpcScreenshotRowOrder>());
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
        Assert.Equal(["enter", "exit"], TextVocabulary.GetTexts<IpcPlayTransitionCommand>());
        Assert.Equal(
            ["entered", "alreadyEntered", "exited", "alreadyExited", "timeout", "blocked"],
            TextVocabulary.GetTexts<IpcPlayTransitionOutcome>());
        Assert.Equal(
            ["notApplied", "applied", "indeterminate", "unknown"],
            TextVocabulary.GetTexts<IpcApplicationState>());
        Assert.False(TextVocabulary.IsDefined((IpcPlayTransitionCommand)0));
        Assert.False(TextVocabulary.IsDefined((IpcPlayTransitionOutcome)0));
        Assert.False(TextVocabulary.IsDefined((IpcApplicationState)0));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteLiteralEnums_ExposeExpectedLiterals ()
    {
        Assert.Equal(["info", "warning", "error"], TextVocabulary.GetTexts<UcliDiagnosticSeverity>());
        Assert.Equal(["none", "partial", "indeterminate"], TextVocabulary.GetTexts<IpcExecuteDiagnosticCoverageImpact>());
        Assert.Equal(["validate", "plan", "call", "skipped"], TextVocabulary.GetTexts<IpcExecuteOperationPhase>());
        Assert.Equal(["assetSearch", "guidPath", "sceneTreeLite"], TextVocabulary.GetTexts<IpcExecuteReadPostconditionSurface>());
        Assert.Equal(["edit", "operation", "refresh"], TextVocabulary.GetTexts<IpcExecutePostReadSourceKind>());
        Assert.Equal(["none", "context", "project"], TextVocabulary.GetTexts<IpcExecutePostReadCommit>());
        Assert.Equal(["deterministic", "unavailable"], TextVocabulary.GetTexts<IpcExecuteExpectedPostState>());

        Assert.False(TextVocabulary.IsDefined((UcliDiagnosticSeverity)0));
        Assert.False(TextVocabulary.IsDefined((IpcExecuteDiagnosticCoverageImpact)0));
        Assert.False(TextVocabulary.IsDefined((IpcExecuteOperationPhase)0));
        Assert.False(TextVocabulary.IsDefined((IpcExecuteReadPostconditionSurface)0));
        Assert.False(TextVocabulary.IsDefined((IpcExecutePostReadSourceKind)0));
        Assert.False(TextVocabulary.IsDefined((IpcExecutePostReadCommit)0));
        Assert.False(TextVocabulary.IsDefined((IpcExecuteExpectedPostState)0));
    }
}
