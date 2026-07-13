using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Infrastructure.Storage;
using MackySoft.Ucli.Unity.Runtime;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates session tokens against persisted <c>session.json</c> content. </summary>
    internal sealed class FileBackedSessionTokenValidator : ISessionTokenValidator
    {
        private readonly object syncRoot = new object();

        private readonly string sessionPath;

        private CachedSessionToken? cachedSessionToken;

        /// <summary> Initializes a new instance of the <see cref="FileBackedSessionTokenValidator" /> class. </summary>
        /// <param name="sessionPath"> The absolute path to daemon <c>session.json</c>. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="sessionPath" /> is <see langword="null" />, empty, or whitespace. </exception>
        public FileBackedSessionTokenValidator (string sessionPath)
        {
            if (string.IsNullOrWhiteSpace(sessionPath))
            {
                throw new ArgumentException("Session path must not be empty.", nameof(sessionPath));
            }

            this.sessionPath = sessionPath;
        }

        /// <summary> Validates one session token value by reading persisted daemon session metadata. </summary>
        /// <param name="sessionToken"> The token presented by client connection. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by operation pipelines. </param>
        /// <returns> <see langword="true" /> when the token matches persisted session token; otherwise <see langword="false" />. </returns>
        public Task<bool> ValidateAsync (
            string? sessionToken,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IpcSessionToken.IsValidEncodedValue(sessionToken))
            {
                return CachedTask.FromResult(false);
            }

            // Session replacement uses atomic rename. The shared reader keeps delete sharing enabled so
            // a probe cannot prevent GUI rebootstrap from publishing the rotated session token on Windows.
            var json = FileUtilities.ReadAllTextOrNull(sessionPath);
            if (json == null)
            {
                ClearCachedToken();
                return CachedTask.FromResult(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (TryReadCachedToken(json, out var cachedToken))
            {
                return CachedTask.FromResult(cachedToken.Matches(sessionToken));
            }

            using var sessionJson = JsonDocument.Parse(json);
            if (!SessionTokenContractReader.TryReadSessionToken(
                    sessionJson.RootElement,
                    out var persistedTokenText,
                    out _))
            {
                ClearCachedToken();
                return CachedTask.FromResult(false);
            }

            if (!IpcSessionToken.TryParse(persistedTokenText, out var persistedToken))
            {
                ClearCachedToken();
                return CachedTask.FromResult(false);
            }

            CacheToken(json, persistedToken);
            return CachedTask.FromResult(persistedToken.Matches(sessionToken));
        }

        private bool TryReadCachedToken (
            string sessionJson,
            out IpcSessionToken? sessionToken)
        {
            lock (syncRoot)
            {
                if (cachedSessionToken != null
                    && string.Equals(cachedSessionToken.SessionJson, sessionJson, StringComparison.Ordinal))
                {
                    sessionToken = cachedSessionToken.SessionToken;
                    return true;
                }
            }

            sessionToken = null;
            return false;
        }

        private void CacheToken (
            string sessionJson,
            IpcSessionToken sessionToken)
        {
            lock (syncRoot)
            {
                cachedSessionToken = new CachedSessionToken(
                    sessionJson,
                    sessionToken);
            }
        }

        private void ClearCachedToken ()
        {
            lock (syncRoot)
            {
                cachedSessionToken = null;
            }
        }

        private sealed class CachedSessionToken
        {
            public CachedSessionToken (
                string sessionJson,
                IpcSessionToken sessionToken)
            {
                SessionJson = sessionJson ?? throw new ArgumentNullException(nameof(sessionJson));
                SessionToken = sessionToken ?? throw new ArgumentNullException(nameof(sessionToken));
            }

            public string SessionJson { get; }

            public IpcSessionToken SessionToken { get; }
        }
    }
}
