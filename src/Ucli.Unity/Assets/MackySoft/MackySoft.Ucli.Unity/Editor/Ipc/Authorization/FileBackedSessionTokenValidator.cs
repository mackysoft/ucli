using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;
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
        public async Task<bool> ValidateAsync (
            string? sessionToken,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IpcSessionToken.IsValidEncodedValue(sessionToken))
            {
                return false;
            }

            // Session replacement uses atomic rename. The shared reader keeps delete sharing enabled so
            // a probe cannot prevent GUI rebootstrap from publishing the rotated session token on Windows.
            ReadOnlyMemory<byte>? serializedSession;
            try
            {
                serializedSession = await FileUtilities.ReadBytesOrNullWithinLimitAsync(
                        sessionPath,
                        DaemonSessionStorageContract.MaximumFileSizeBytes,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                ClearCachedToken();
                return false;
            }

            if (serializedSession == null)
            {
                ClearCachedToken();
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var artifactIdentity = Sha256Digest.Compute(serializedSession.Value.Span);
            if (TryReadCachedToken(artifactIdentity, out var cachedToken))
            {
                return cachedToken.Matches(sessionToken);
            }

            try
            {
                using var sessionJson = JsonDocument.Parse(serializedSession.Value);
                if (!SessionTokenContractReader.TryReadSessionToken(
                        sessionJson.RootElement,
                        out var persistedTokenText,
                        out _)
                    || !IpcSessionToken.TryParse(persistedTokenText, out var persistedToken))
                {
                    ClearCachedToken();
                    return false;
                }

                CacheToken(artifactIdentity, persistedToken);
                return persistedToken.Matches(sessionToken);
            }
            catch (JsonException)
            {
                ClearCachedToken();
                return false;
            }
        }

        private bool TryReadCachedToken (
            Sha256Digest artifactIdentity,
            out IpcSessionToken? sessionToken)
        {
            lock (syncRoot)
            {
                if (cachedSessionToken != null
                    && cachedSessionToken.ArtifactIdentity == artifactIdentity)
                {
                    sessionToken = cachedSessionToken.SessionToken;
                    return true;
                }
            }

            sessionToken = null;
            return false;
        }

        private void CacheToken (
            Sha256Digest artifactIdentity,
            IpcSessionToken sessionToken)
        {
            lock (syncRoot)
            {
                cachedSessionToken = new CachedSessionToken(
                    artifactIdentity,
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
                Sha256Digest artifactIdentity,
                IpcSessionToken sessionToken)
            {
                ArtifactIdentity = artifactIdentity ?? throw new ArgumentNullException(nameof(artifactIdentity));
                SessionToken = sessionToken ?? throw new ArgumentNullException(nameof(sessionToken));
            }

            public Sha256Digest ArtifactIdentity { get; }

            public IpcSessionToken SessionToken { get; }
        }
    }
}
