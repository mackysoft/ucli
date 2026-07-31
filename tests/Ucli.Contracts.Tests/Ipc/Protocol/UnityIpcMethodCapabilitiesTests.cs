using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

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
    [InlineData(UnityIpcMethod.LifecycleStart, true)]
    [InlineData(UnityIpcMethod.Refresh, true)]
    [InlineData(UnityIpcMethod.Compile, true)]
    [InlineData(UnityIpcMethod.PlayEnter, true)]
    [InlineData(UnityIpcMethod.PlayExit, true)]
    [InlineData(UnityIpcMethod.BuildRun, false)]
    [InlineData(UnityIpcMethod.TestRun, false)]
    public void SupportsLifecycleExecution_ReturnsMethodCapability (
        UnityIpcMethod method,
        bool expected)
    {
        Assert.Equal(expected, UnityIpcMethodCapabilities.SupportsLifecycleExecution(method));
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
    public void LifecycleExecutionMethods_DoNotSupportStreamingOrStatelessReplay ()
    {
        foreach (var method in Enum.GetValues<UnityIpcMethod>())
        {
            Assert.False(
                UnityIpcMethodCapabilities.SupportsLifecycleExecution(method)
                && (UnityIpcMethodCapabilities.SupportsStreaming(method)
                    || UnityIpcMethodCapabilities.SupportsStatelessReadReplay(method)),
                $"Lifecycle Execution method '{method}' cannot combine durable execution replay with streaming or stateless read replay.");
        }
    }

    [Theory]
    [Trait("Size", "Small")]
    [InlineData(UnityIpcMethod.Compile, UnityEditorLifecycleState.CompileFailed, true)]
    [InlineData(UnityIpcMethod.Compile, UnityEditorLifecycleState.SafeMode, true)]
    [InlineData(UnityIpcMethod.Compile, UnityEditorLifecycleState.Ready, false)]
    [InlineData(UnityIpcMethod.OpsRead, UnityEditorLifecycleState.CompileFailed, false)]
    public void AllowsStartupLifecycleState_ReturnsMethodCapability (
        UnityIpcMethod method,
        UnityEditorLifecycleState lifecycleState,
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
        Assert.Throws<ArgumentOutOfRangeException>(() => UnityIpcMethodCapabilities.SupportsLifecycleExecution(method));
        Assert.Throws<ArgumentOutOfRangeException>(() => UnityIpcMethodCapabilities.SupportsStatelessReadReplay(method));
        Assert.Throws<ArgumentOutOfRangeException>(() => UnityIpcMethodCapabilities.AllowsStartupLifecycleState(
            method,
            UnityEditorLifecycleState.Ready));
    }
}
