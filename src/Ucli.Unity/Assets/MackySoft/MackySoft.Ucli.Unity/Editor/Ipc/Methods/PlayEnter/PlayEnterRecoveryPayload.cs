using System;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Captures the snapshot needed to resume a recoverable Play Mode enter request. </summary>
    internal sealed class PlayEnterRecoveryPayload
    {
        /// <summary> Initializes a new instance of the <see cref="PlayEnterRecoveryPayload" /> class. </summary>
        public PlayEnterRecoveryPayload ()
        {
        }

        /// <summary> Initializes a new instance of the <see cref="PlayEnterRecoveryPayload" /> class. </summary>
        public PlayEnterRecoveryPayload (IpcUnityEditorObservation before)
        {
            Before = before ?? throw new ArgumentNullException(nameof(before));
        }

        /// <summary> Gets or sets the lifecycle snapshot captured before requesting Play Mode. </summary>
        public IpcUnityEditorObservation Before { get; set; }
    }
}
