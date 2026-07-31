using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Editor;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Exposes only the Play Mode entry checkpoint decisions needed by its transition runner.
    /// </summary>
    internal interface IPlayEnterLifecycleExecutionContext
    {
        /// <summary>
        /// Gets whether the entry side effect was admitted after its before snapshot became durable.
        /// </summary>
        bool HasSideEffectAdmission { get; }

        /// <summary> Reads the durable before snapshot of an admitted transition. </summary>
        bool TryReadBefore (
            out UnityEditorObservation before,
            out string errorMessage);

        /// <summary>
        /// Makes the before snapshot durable and tries to acquire the execution's entry-side-effect admission.
        /// </summary>
        /// <returns>
        /// <see langword="true" /> only when this caller durably changed the execution
        /// from <c>registered</c> to <c>entering</c>; otherwise,
        /// <see langword="false" /> so the caller recovers from durable evidence.
        /// </returns>
        ValueTask<bool> TryAdmitSideEffectAsync (
            UnityEditorObservation before,
            CancellationToken cancellationToken);
    }
}
