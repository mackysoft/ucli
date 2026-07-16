using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc.Authorization;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates whether an incoming session token is acceptable for IPC requests. </summary>
    internal interface ISessionTokenValidator
    {
        /// <summary> Validates one parsed incoming session token. </summary>
        /// <param name="sessionToken"> The validated token presented by the client connection. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> <see langword="true" /> when token is accepted; otherwise <see langword="false" />. </returns>
        Task<bool> ValidateAsync (
            IpcSessionToken sessionToken,
            CancellationToken cancellationToken = default);
    }
}
