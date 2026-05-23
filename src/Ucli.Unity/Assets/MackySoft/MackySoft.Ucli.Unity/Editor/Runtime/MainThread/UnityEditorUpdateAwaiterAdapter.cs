using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Runtime
{
    /// <summary> Adapts the static Unity Editor update awaiter for dependency injection. </summary>
    internal sealed class UnityEditorUpdateAwaiterAdapter : IUnityEditorUpdateAwaiter
    {
        /// <inheritdoc />
        public Task WaitForNextUpdateAsync (CancellationToken cancellationToken)
        {
            return UnityEditorUpdateAwaiter.WaitForNextUpdateAsync(cancellationToken);
        }
    }
}
