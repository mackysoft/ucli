using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.UnityIntegration.Ipc.Clients;
using MackySoft.Ucli.UnityIntegration.Ipc.Execution;

namespace MackySoft.Ucli.Tests.Ipc;

public sealed class UnityIpcClientSelectorTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Select_WithRegisteredTarget_ReturnsMatchingClient ()
    {
        var daemonClient = new StubUnityIpcClient(UnityExecutionTarget.Daemon);
        var oneshotClient = new StubUnityIpcClient(UnityExecutionTarget.Oneshot);
        var selector = new UnityIpcClientSelector([daemonClient, oneshotClient]);

        var selected = selector.Select(UnityExecutionTarget.Oneshot);

        Assert.Same(oneshotClient, selected);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Select_WithMissingTarget_ThrowsInvalidOperationException ()
    {
        var selector = new UnityIpcClientSelector([new StubUnityIpcClient(UnityExecutionTarget.Daemon)]);

        var exception = Assert.Throws<InvalidOperationException>(() => selector.Select(UnityExecutionTarget.Oneshot));

        Assert.Contains("Oneshot", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithDuplicateTarget_ThrowsInvalidOperationException ()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new UnityIpcClientSelector(
        [
            new StubUnityIpcClient(UnityExecutionTarget.Daemon),
            new StubUnityIpcClient(UnityExecutionTarget.Daemon),
        ]));

        Assert.Contains("Daemon", exception.Message, StringComparison.Ordinal);
    }

    private sealed class StubUnityIpcClient : IUnityIpcClient
    {
        public StubUnityIpcClient (UnityExecutionTarget target)
        {
            Target = target;
        }

        public UnityExecutionTarget Target { get; }

        public ValueTask<UnityRequestExecutionResult> SendAsync (
            ResolvedUnityProjectContext unityProject,
            UnityIpcDispatchRequest dispatchRequest,
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
