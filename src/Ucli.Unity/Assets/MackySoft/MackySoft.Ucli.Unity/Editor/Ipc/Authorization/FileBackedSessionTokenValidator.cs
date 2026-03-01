using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Validates session tokens against persisted <c>session.json</c> content. </summary>
    internal sealed class FileBackedSessionTokenValidator : ISessionTokenValidator
    {
        private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false,
        };

        private readonly string sessionPath;

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
        public async Task<bool> Validate (
            string sessionToken,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(sessionToken))
            {
                return false;
            }

            if (!File.Exists(sessionPath))
            {
                return false;
            }

            try
            {
                var json = await File.ReadAllTextAsync(sessionPath, cancellationToken);
                var sessionTokenEntry = JsonSerializer.Deserialize<SessionTokenEntry>(json, SerializerOptions);
                if (sessionTokenEntry == null || string.IsNullOrWhiteSpace(sessionTokenEntry.SessionToken))
                {
                    return false;
                }

                return string.Equals(sessionTokenEntry.SessionToken, sessionToken, StringComparison.Ordinal);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return false;
            }
        }

        /// <summary> Represents JSON fields required for token validation. </summary>
        /// <param name="SessionToken"> The daemon session token from persistence file. </param>
        private sealed record SessionTokenEntry (string? SessionToken);
    }
}
