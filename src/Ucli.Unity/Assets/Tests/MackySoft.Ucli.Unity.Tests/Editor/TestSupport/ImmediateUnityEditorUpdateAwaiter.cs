using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Tests
{
    /// <summary> Completes Editor update checkpoints immediately in tests that do not exercise update timing. </summary>
    internal sealed class ImmediateUnityEditorUpdateAwaiter : IUnityEditorUpdateAwaiter
    {
        public Task WaitForNextUpdateAsync (CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }
}
