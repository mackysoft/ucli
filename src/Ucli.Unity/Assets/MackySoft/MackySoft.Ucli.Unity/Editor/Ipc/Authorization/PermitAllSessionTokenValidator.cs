using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Temporary token validator used before persistent session management is introduced. </summary>
    internal sealed class PermitAllSessionTokenValidator : ISessionTokenValidator
    {
        /// <summary> Accepts one parsed session token. </summary>
        /// <param name="sessionToken"> The validated token presented by the client connection. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> Always returns <see langword="true" />. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="sessionToken" /> is <see langword="null" />. </exception>
        public Task<bool> ValidateAsync (
            IpcSessionToken sessionToken,
            CancellationToken cancellationToken = default)
        {
            if (sessionToken == null)
            {
                throw new ArgumentNullException(nameof(sessionToken));
            }

            cancellationToken.ThrowIfCancellationRequested();
            return CachedTask.FromResult(true);
        }
    }
}
