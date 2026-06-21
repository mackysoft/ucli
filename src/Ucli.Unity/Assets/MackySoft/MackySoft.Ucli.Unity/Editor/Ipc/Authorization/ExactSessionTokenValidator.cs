using System;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates one session token against one expected exact literal. </summary>
    internal sealed class ExactSessionTokenValidator : ISessionTokenValidator
    {
        private readonly string expectedSessionToken;

        /// <summary> Initializes a new instance of the <see cref="ExactSessionTokenValidator" /> class. </summary>
        /// <param name="expectedSessionToken"> The exact token value accepted by the validator. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="expectedSessionToken" /> is <see langword="null" />, empty, or whitespace. </exception>
        public ExactSessionTokenValidator (string expectedSessionToken)
        {
            if (string.IsNullOrWhiteSpace(expectedSessionToken))
            {
                throw new ArgumentException("Expected session token must not be empty.", nameof(expectedSessionToken));
            }

            this.expectedSessionToken = expectedSessionToken;
        }

        /// <summary> Validates one presented session token against the expected exact literal. </summary>
        /// <param name="sessionToken"> The token presented by client connection. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> <see langword="true" /> when the token exactly matches the expected value; otherwise <see langword="false" />. </returns>
        public Task<bool> ValidateAsync (
            string sessionToken,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var accepted = string.Equals(expectedSessionToken, sessionToken, StringComparison.Ordinal);
            return CachedTask.FromResult(accepted);
        }
    }
}
