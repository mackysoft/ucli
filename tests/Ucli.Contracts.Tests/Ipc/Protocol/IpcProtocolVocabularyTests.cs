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
        Assert.Equal("ok", IpcProtocol.StatusOk);
        Assert.Equal("error", IpcProtocol.StatusError);
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
    public void UnityIpcMethod_WhenUnspecified_IsNotMapped ()
    {
        Assert.Equal(0, (int)UnityIpcMethod.Unspecified);
        Assert.False(ContractLiteralCodec.IsDefined(UnityIpcMethod.Unspecified));
        Assert.False(ContractLiteralCodec.TryToValue(UnityIpcMethod.Unspecified, out var literal));
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
        Assert.Equal(UnityIpcMethod.Unspecified, method);
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
