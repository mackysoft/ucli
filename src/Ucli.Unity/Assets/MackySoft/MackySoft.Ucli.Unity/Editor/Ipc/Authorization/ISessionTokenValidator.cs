using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates whether an incoming session token is acceptable for IPC requests. </summary>
    internal interface ISessionTokenValidator
    {
        /// <summary> Validates one incoming session token value. </summary>
        /// <param name="sessionToken"> The token presented by client connection. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> <see langword="true" /> when token is accepted; otherwise <see langword="false" />. </returns>
        Task<bool> ValidateAsync (
            string sessionToken,
            CancellationToken cancellationToken = default);
    }
}