using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Defines recoverable IPC operation states used by daemon replay logic. </summary>
    [VocabularyDefinition]
    internal enum RecoverableIpcOperationState
    {
        /// <summary> The operation has started and may be resumed after a domain reload. </summary>
        [VocabularyText("pending")]
        Pending,

        /// <summary> The operation response has been durably recorded and can be replayed. </summary>
        [VocabularyText("completed")]
        Completed,
    }
}
