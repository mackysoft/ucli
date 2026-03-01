using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Temporary token validator used before persistent session management is introduced. </summary>
    internal sealed class PermitAllSessionTokenValidator : ISessionTokenValidator
    {
        /// <summary> Validates one session token value. </summary>
        /// <param name="sessionToken"> The token presented by client connection. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> Always returns <see langword="true" />. </returns>
        public Task<bool> Validate (
            string sessionToken,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(true);
        }
    }
}
