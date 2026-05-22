using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Persists Play Mode enter transition state across Unity domain reload. </summary>
    internal interface IPlayEnterPendingTransitionStore
    {
        /// <summary> Writes the pre-transition snapshot used to recover a lost enter response. </summary>
        bool TryWrite (
            IpcPlayLifecycleSnapshot before,
            out string errorMessage);

        /// <summary> Reads the pre-transition snapshot when one is pending. </summary>
        bool TryRead (
            out IpcPlayLifecycleSnapshot before,
            out string errorMessage);

        /// <summary> Removes pending transition state. </summary>
        bool TryDelete (out string errorMessage);
    }
}
