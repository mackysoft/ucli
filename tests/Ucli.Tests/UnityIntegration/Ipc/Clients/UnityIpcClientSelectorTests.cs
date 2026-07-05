using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Tests.Helpers.Ipc;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcClientSelectorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Select_WithRegisteredTarget_ReturnsMatchingClient ()
    {
        var daemonClient = new RecordingUnityIpcClient(UnityExecutionTarget.Daemon);
        var oneshotClient = new RecordingUnityIpcClient(UnityExecutionTarget.Oneshot);
        var selector = new UnityIpcClientSelector([daemonClient, oneshotClient]);

        var selected = selector.Select(UnityExecutionTarget.Oneshot);

        Assert.Same(oneshotClient, selected);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Select_WithMissingTarget_ThrowsInvalidOperationException ()
    {
        var selector = new UnityIpcClientSelector([new RecordingUnityIpcClient(UnityExecutionTarget.Daemon)]);

        var exception = Assert.Throws<InvalidOperationException>(() => selector.Select(UnityExecutionTarget.Oneshot));

        Assert.Contains("Oneshot", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithDuplicateTarget_ThrowsInvalidOperationException ()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new UnityIpcClientSelector(
        [
            new RecordingUnityIpcClient(UnityExecutionTarget.Daemon),
            new RecordingUnityIpcClient(UnityExecutionTarget.Daemon),
        ]));

        Assert.Contains("Daemon", exception.Message, StringComparison.Ordinal);
    }
}
