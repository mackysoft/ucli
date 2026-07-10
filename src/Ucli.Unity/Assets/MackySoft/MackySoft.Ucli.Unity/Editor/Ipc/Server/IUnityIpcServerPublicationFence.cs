using System;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Fences durable endpoint publication against one listener generation's termination. </summary>
    internal interface IUnityIpcServerPublicationFence : IDisposable
    {
        /// <summary> Throws when the listener generation can no longer accept durable publication. </summary>
        /// <exception cref="InvalidOperationException"> Thrown when the listener generation has terminated or lost ownership. </exception>
        void ThrowIfGenerationTerminated ();

        /// <summary> Atomically transfers durable publication ownership while the listener generation is still active. </summary>
        /// <param name="commitActiveOwnership"> The synchronous ownership transfer performed under the server generation fence. </param>
        /// <returns> <see langword="true" /> when ownership was transferred; otherwise, <see langword="false" />. </returns>
        bool TryCommitActiveOwnership (Action commitActiveOwnership);
    }
}
