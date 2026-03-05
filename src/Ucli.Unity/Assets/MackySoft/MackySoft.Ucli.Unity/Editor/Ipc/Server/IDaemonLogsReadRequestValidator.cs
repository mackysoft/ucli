using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates and normalizes daemon-log read payload values. </summary>
    internal interface IDaemonLogsReadRequestValidator
    {
        /// <summary> Validates one daemon-log read payload against current stream context. </summary>
        /// <param name="request"> The daemon-log read payload. </param>
        /// <param name="currentStreamId"> The current stream identifier. </param>
        /// <param name="filter"> The normalized filter values when validation succeeds. </param>
        /// <param name="errorMessage"> The invalid-argument message when validation fails. </param>
        /// <returns> <see langword="true" /> when payload is valid; otherwise <see langword="false" />. </returns>
        bool TryValidate (
            IpcDaemonLogsReadRequest request,
            string currentStreamId,
            out DaemonLogsReadFilter filter,
            out string errorMessage);
    }
}
