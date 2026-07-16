using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates one parsed session token against the expected token. </summary>
    internal sealed class ExactSessionTokenValidator : ISessionTokenValidator
    {
        private readonly IpcSessionToken expectedSessionToken;

        /// <summary> Initializes a new instance of the <see cref="ExactSessionTokenValidator" /> class. </summary>
        /// <param name="expectedSessionToken"> The validated token accepted by the validator. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="expectedSessionToken" /> is <see langword="null" />. </exception>
        public ExactSessionTokenValidator (IpcSessionToken expectedSessionToken)
        {
            this.expectedSessionToken = expectedSessionToken
                ?? throw new ArgumentNullException(nameof(expectedSessionToken));
        }

        /// <summary> Validates one parsed session token against the expected token. </summary>
        /// <param name="sessionToken"> The validated token presented by the client connection. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> <see langword="true" /> when the token exactly matches the expected value; otherwise <see langword="false" />. </returns>
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
            return CachedTask.FromResult(expectedSessionToken == sessionToken);
        }
    }
}
