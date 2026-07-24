using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts.Daemon;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Ipc.Authorization;
using MackySoft.Ucli.Contracts.Storage;
using MackySoft.Ucli.Infrastructure.Storage;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Resolves and validates the immutable session token owned by one batchmode daemon bootstrap generation. </summary>
    internal static class DaemonBootstrapSessionTokenResolver
    {
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true);

        /// <summary> Reads the canonical daemon session once and returns the token bound to the bootstrap generation. </summary>
        /// <param name="bootstrapContext"> The guarded batchmode daemon identity and session generation. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by daemon bootstrap. </param>
        /// <returns> The validated token owned by the bootstrap generation. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="bootstrapContext" /> is <see langword="null" />. </exception>
        /// <exception cref="InvalidOperationException"> Thrown when the declared session path is not canonical or the session file does not exist. </exception>
        /// <exception cref="InvalidDataException"> Thrown when the persisted session is malformed or belongs to another bootstrap generation. </exception>
        public static async Task<IpcSessionToken> ResolveAsync (
            UnityDaemonBootstrapContext bootstrapContext,
            CancellationToken cancellationToken)
        {
            if (bootstrapContext == null)
            {
                throw new ArgumentNullException(nameof(bootstrapContext));
            }

            cancellationToken.ThrowIfCancellationRequested();
            var canonicalSessionPath = UcliStoragePathResolver.ResolveSessionPath(
                bootstrapContext.RepositoryRoot,
                bootstrapContext.ProjectFingerprint);
            if (!bootstrapContext.SessionPath.IsSameAs(canonicalSessionPath))
            {
                throw new InvalidOperationException(
                    "Daemon bootstrap session path does not match the canonical project session path.");
            }

            var serializedSession = await FileUtilities.ReadBytesOrNullWithinLimitAsync(
                    canonicalSessionPath,
                    DaemonSessionStorageContract.MaximumFileSizeBytes,
                    cancellationToken)
                .ConfigureAwait(false);
            if (serializedSession == null)
            {
                throw new InvalidOperationException(
                    "Daemon bootstrap session file does not exist at the canonical project session path.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!TryResolveExpectedGenerationToken(
                bootstrapContext,
                serializedSession.Value,
                out var sessionToken))
            {
                throw new InvalidDataException(
                    "Persisted daemon session does not match the batchmode bootstrap generation.");
            }

            return sessionToken;
        }

        private static bool TryResolveExpectedGenerationToken (
            UnityDaemonBootstrapContext bootstrapContext,
            ReadOnlyMemory<byte> serializedSession,
            out IpcSessionToken sessionToken)
        {
            sessionToken = null;
            DaemonSessionJsonContract sessionContract;
            try
            {
                var json = StrictUtf8.GetString(serializedSession.Span);
                sessionContract = DaemonSessionJsonContractSerializer.Deserialize(json);
            }
            catch (Exception exception) when (exception is ArgumentException or JsonException)
            {
                return false;
            }

            if (sessionContract == null
                || sessionContract.SchemaVersion != DaemonSessionStorageContract.CurrentSchemaVersion
                || sessionContract.SessionGenerationId != bootstrapContext.SessionGenerationId
                || sessionContract.ProjectFingerprint != bootstrapContext.ProjectFingerprint
                || sessionContract.IssuedAtUtc.Offset != TimeSpan.Zero
                || sessionContract.IssuedAtUtc != bootstrapContext.SessionIssuedAtUtc
                || sessionContract.EditorMode != DaemonEditorMode.Batchmode
                || sessionContract.OwnerKind != DaemonSessionOwnerKind.Cli
                || !sessionContract.CanShutdownProcess
                || sessionContract.EndpointTransportKind is not IpcTransportKind endpointTransportKind)
            {
                return false;
            }

            IpcEndpoint persistedEndpoint;
            try
            {
                persistedEndpoint = new IpcEndpoint(
                    endpointTransportKind,
                    sessionContract.EndpointAddress);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return persistedEndpoint == bootstrapContext.EndpointBinding.Endpoint
                && IpcSessionToken.TryParse(sessionContract.SessionToken, out sessionToken);
        }
    }
}
