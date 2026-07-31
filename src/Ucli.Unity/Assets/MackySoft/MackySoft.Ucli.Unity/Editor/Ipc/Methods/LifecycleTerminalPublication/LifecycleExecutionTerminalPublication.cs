using System;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Carries one valid delivery outcome for a fixed, action-typed Lifecycle Execution
    /// Terminal Record.
    /// </summary>
    internal abstract class LifecycleExecutionTerminalPublication<TTerminalRecord>
        where TTerminalRecord : LifecycleExecutionTerminalRecord
    {
        private LifecycleExecutionTerminalPublication ()
        {
        }

        internal sealed class Verified :
            LifecycleExecutionTerminalPublication<TTerminalRecord>
        {
            public Verified (
                TerminalExecutionRef terminalReference,
                TTerminalRecord terminalRecord)
            {
                TerminalReference = terminalReference
                    ?? throw new ArgumentNullException(nameof(terminalReference));
                TerminalRecord = terminalRecord
                    ?? throw new ArgumentNullException(nameof(terminalRecord));
            }

            public TerminalExecutionRef TerminalReference { get; }

            public TTerminalRecord TerminalRecord { get; }
        }

        internal sealed class PublicationFailed :
            LifecycleExecutionTerminalPublication<TTerminalRecord>
        {
            public PublicationFailed (
                ExecutionRef reconnectableReference,
                TTerminalRecord terminalRecord)
            {
                ReconnectableReference = reconnectableReference
                    ?? throw new ArgumentNullException(
                        nameof(reconnectableReference));
                TerminalRecord = terminalRecord
                    ?? throw new ArgumentNullException(nameof(terminalRecord));
            }

            public ExecutionRef ReconnectableReference { get; }

            public TTerminalRecord TerminalRecord { get; }
        }

        internal sealed class Unavailable :
            LifecycleExecutionTerminalPublication<TTerminalRecord>
        {
            public Unavailable (ExecutionRef reconnectableReference)
            {
                ReconnectableReference = reconnectableReference
                    ?? throw new ArgumentNullException(
                        nameof(reconnectableReference));
            }

            public ExecutionRef ReconnectableReference { get; }
        }
    }
}
