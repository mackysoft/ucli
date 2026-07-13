using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Captures the snapshot needed to resume a recoverable Play Mode exit request. </summary>
    internal sealed class PlayExitRecoveryPayload
    {
        /// <summary> Initializes a new instance of the <see cref="PlayExitRecoveryPayload" /> class. </summary>
        public PlayExitRecoveryPayload ()
        {
        }

        /// <summary> Initializes a new instance of the <see cref="PlayExitRecoveryPayload" /> class. </summary>
        public PlayExitRecoveryPayload (IpcUnityEditorObservation before)
        {
            Before = before ?? throw new ArgumentNullException(nameof(before));
        }

        /// <summary> Gets or sets the lifecycle snapshot captured before requesting Play Mode exit. </summary>
        public IpcUnityEditorObservation Before { get; set; }
    }
}
