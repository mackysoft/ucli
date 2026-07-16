using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Ipc;

public sealed class UnityIpcMethodCapabilitiesTests
{
    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UnityIpcMethod.BuildRun, true)]
    [InlineData(UnityIpcMethod.TestRun, true)]
    [InlineData(UnityIpcMethod.Compile, false)]
    [InlineData(UnityIpcMethod.OpsRead, false)]
    public void SupportsStreaming_ReturnsMethodCapability (
        UnityIpcMethod method,
        bool expected)
    {
        Assert.Equal(expected, UnityIpcMethodCapabilities.SupportsStreaming(method));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UnityIpcMethod.Compile, true)]
    [InlineData(UnityIpcMethod.PlayEnter, true)]
    [InlineData(UnityIpcMethod.PlayExit, true)]
    [InlineData(UnityIpcMethod.BuildRun, false)]
    [InlineData(UnityIpcMethod.TestRun, false)]
    public void SupportsRecoverableReplay_ReturnsMethodCapability (
        UnityIpcMethod method,
        bool expected)
    {
        Assert.Equal(expected, UnityIpcMethodCapabilities.SupportsRecoverableReplay(method));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UnityIpcMethod.Ping, true)]
    [InlineData(UnityIpcMethod.OpsRead, true)]
    [InlineData(UnityIpcMethod.IndexAssetsRead, true)]
    [InlineData(UnityIpcMethod.IndexSceneTreeLiteRead, true)]
    [InlineData(UnityIpcMethod.DaemonLogsRead, true)]
    [InlineData(UnityIpcMethod.UnityLogsRead, true)]
    [InlineData(UnityIpcMethod.PlayStatus, true)]
    [InlineData(UnityIpcMethod.Execute, false)]
    [InlineData(UnityIpcMethod.Compile, false)]
    [InlineData(UnityIpcMethod.UnityConsoleClear, false)]
    [InlineData(UnityIpcMethod.ScreenshotCapture, false)]
    public void SupportsStatelessReadReplay_ReturnsMethodCapability (
        UnityIpcMethod method,
        bool expected)
    {
        Assert.Equal(expected, UnityIpcMethodCapabilities.SupportsStatelessReadReplay(method));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void RecoverableReplayMethods_DoNotSupportStreaming ()
    {
        foreach (var method in Enum.GetValues<UnityIpcMethod>())
        {
            Assert.False(
                UnityIpcMethodCapabilities.SupportsRecoverableReplay(method)
                && (UnityIpcMethodCapabilities.SupportsStreaming(method)
                    || UnityIpcMethodCapabilities.SupportsStatelessReadReplay(method)),
                $"Method '{method}' cannot combine durable execution replay with streaming or stateless read replay.");
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UnityIpcMethod.Compile, IpcEditorLifecycleState.CompileFailed, true)]
    [InlineData(UnityIpcMethod.Compile, IpcEditorLifecycleState.SafeMode, true)]
    [InlineData(UnityIpcMethod.Compile, IpcEditorLifecycleState.Ready, false)]
    [InlineData(UnityIpcMethod.OpsRead, IpcEditorLifecycleState.CompileFailed, false)]
    public void AllowsStartupLifecycleState_ReturnsMethodCapability (
        UnityIpcMethod method,
        IpcEditorLifecycleState lifecycleState,
        bool expected)
    {
        Assert.Equal(expected, UnityIpcMethodCapabilities.AllowsStartupLifecycleState(method, lifecycleState));
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    public void Capabilities_WhenMethodIsUndefined_ThrowArgumentOutOfRangeException (int value)
    {
        var method = (UnityIpcMethod)value;

        Assert.Throws<ArgumentOutOfRangeException>(() => UnityIpcMethodCapabilities.SupportsStreaming(method));
        Assert.Throws<ArgumentOutOfRangeException>(() => UnityIpcMethodCapabilities.SupportsRecoverableReplay(method));
        Assert.Throws<ArgumentOutOfRangeException>(() => UnityIpcMethodCapabilities.SupportsStatelessReadReplay(method));
        Assert.Throws<ArgumentOutOfRangeException>(() => UnityIpcMethodCapabilities.AllowsStartupLifecycleState(
            method,
            IpcEditorLifecycleState.Ready));
    }
}
