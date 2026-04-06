using System;
using System.Collections.Generic;
using System.Threading;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Unity.Execution.Phases;
using MackySoft.Ucli.Unity.Execution.Requests;

#nullable enable

namespace MackySoft.Ucli.Unity.Execution.PlanToken
{
    /// <summary> Provides file-backed plan-token issuance and validation services. </summary>
    internal sealed class PlanTokenCoordinator : IPlanTokenCoordinator
    {
        private static readonly TimeSpan DefaultTokenTtl = TimeSpan.FromMinutes(15);

        private static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

        private readonly IPlanTokenEnvironment environment;

        /// <summary> Initializes a new instance of the <see cref="PlanTokenCoordinator" /> class. </summary>
        /// <param name="environment"> The runtime environment dependency. </param>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="environment" /> is <see langword="null" />. </exception>
        public PlanTokenCoordinator (IPlanTokenEnvironment environment)
        {
            this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        /// <summary> Initializes a new instance of the <see cref="PlanTokenCoordinator" /> class with default runtime environment. </summary>
        public PlanTokenCoordinator () : this(new DefaultPlanTokenEnvironment())
        {
        }

        /// <summary>
        /// Issues one plan token from a normalized request and its plan-phase primitive traces.
        /// </summary>
        /// <param name="request"> The normalized request model. Must not be <see langword="null" />. </param>
        /// <param name="operationTraces"> The plan-phase primitive traces used to derive the state fingerprint. Must not be <see langword="null" />. </param>
        /// <returns> The token issue result that includes request, compiled-execution, and state fingerprints when issuance succeeds. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> or <paramref name="operationTraces" /> is <see langword="null" />. </exception>
        public PlanTokenIssueResult Issue (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            ReadOnlyMemory<byte> compiledDigestPayloadUtf8,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var snapshot = environment.Capture();
                var requestDigest = Sha256LowerHex.Compute(request.CanonicalDigestPayloadUtf8.Span);
                var compiledExecutionDigest = Sha256LowerHex.Compute(compiledDigestPayloadUtf8.Span);
                var stateFingerprint = PlanTokenStateFingerprintCalculator.Compute(snapshot, operationTraces, cancellationToken);

                if (!PlanTokenKeyStore.TryLoadOrCreate(snapshot, out var signingKey, out var keyErrorMessage))
                {
                    return PlanTokenIssueResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.InternalError,
                        Message: keyErrorMessage ?? "Failed to load plan-token signing key.",
                        OpId: null));
                }

                var issuedAtUtc = environment.UtcNow;
                var expiresAtUtc = issuedAtUtc.Add(DefaultTokenTtl);
                var payload = new PlanTokenPayload(
                    Version: PlanTokenCompactCodec.TokenVersion,
                    KeyId: PlanTokenCompactCodec.TokenKeyId,
                    ProjectFingerprint: snapshot.ProjectFingerprint,
                    RequestDigest: requestDigest,
                    CompiledExecutionDigest: compiledExecutionDigest,
                    StateFingerprint: stateFingerprint,
                    IssuedAtUtc: issuedAtUtc,
                    ExpiresAtUtc: expiresAtUtc,
                    Nonce: PlanTokenCompactCodec.CreateNonce());

                var token = PlanTokenCompactCodec.CreateSignedToken(signingKey, payload);
                return PlanTokenIssueResult.Success(token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return PlanTokenIssueResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: $"Failed to issue plan token. {exception.Message}",
                    OpId: null));
            }
        }

        /// <summary>
        /// Validates one incoming call plan token against the current request, compiler output, and project state.
        /// </summary>
        /// <param name="request"> The normalized request model. Must not be <see langword="null" />. </param>
        /// <param name="operationTraces"> The pre-call primitive traces used to recompute the state fingerprint. Must not be <see langword="null" />. </param>
        /// <returns> The validation result. Missing tokens are accepted only when plan tokens are optional for the current project configuration. </returns>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="request" /> or <paramref name="operationTraces" /> is <see langword="null" />. </exception>
        public PlanTokenValidationResult ValidateCallRequest (
            NormalizedExecuteRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                return TryValidateCallRequestCore(
                    request,
                    out _,
                    out _,
                    out var failure,
                    cancellationToken)
                    ? PlanTokenValidationResult.Success()
                    : PlanTokenValidationResult.Failed(failure!);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return PlanTokenValidationResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: $"Failed to validate plan token. {exception.Message}",
                    OpId: null));
            }
        }

        public PlanTokenValidationResult ValidateCall (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
            ReadOnlyMemory<byte> compiledDigestPayloadUtf8,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (operationTraces == null)
            {
                throw new ArgumentNullException(nameof(operationTraces));
            }

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                if (!TryValidateCallRequestCore(
                        request,
                        out var snapshot,
                        out var decodedToken,
                        out var failure,
                        cancellationToken))
                {
                    return PlanTokenValidationResult.Failed(failure!);
                }

                if (decodedToken == null)
                {
                    return PlanTokenValidationResult.Success();
                }

                var payload = decodedToken.Payload;
                if (!string.IsNullOrWhiteSpace(payload.CompiledExecutionDigest))
                {
                    var compiledExecutionDigest = Sha256LowerHex.Compute(compiledDigestPayloadUtf8.Span);
                    if (!string.Equals(compiledExecutionDigest, payload.CompiledExecutionDigest, StringComparison.Ordinal))
                    {
                        return PlanTokenValidationResult.Failed(new OperationFailure(
                            Code: IpcErrorCodes.StateChangedSincePlan,
                            Message: "Compiled execution changed since plan token issuance.",
                            OpId: null));
                    }
                }

                var stateFingerprint = PlanTokenStateFingerprintCalculator.Compute(snapshot, operationTraces, cancellationToken);
                if (!string.Equals(stateFingerprint, payload.StateFingerprint, StringComparison.Ordinal))
                {
                    return PlanTokenValidationResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.StateChangedSincePlan,
                        Message: "Project state changed since plan token issuance.",
                        OpId: null));
                }

                return PlanTokenValidationResult.Success();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return PlanTokenValidationResult.Failed(new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: $"Failed to validate plan token. {exception.Message}",
                    OpId: null));
            }
        }

        private bool TryValidateCallRequestCore (
            NormalizedExecuteRequest request,
            out PlanTokenEnvironmentSnapshot snapshot,
            out PlanTokenDecodedToken? decodedToken,
            out OperationFailure? failure,
            CancellationToken cancellationToken)
        {
            snapshot = environment.Capture();
            decodedToken = null;
            failure = null;

            var config = PlanTokenConfigResolver.Resolve(snapshot.RepositoryRoot);
            if (string.IsNullOrWhiteSpace(request.PlanToken))
            {
                if (config.Mode == PlanTokenMode.Required)
                {
                    failure = new OperationFailure(
                        Code: IpcErrorCodes.PlanTokenRequired,
                        Message: "Plan token is required for call execution.",
                        OpId: null);
                    return false;
                }

                return true;
            }

            if (!PlanTokenCompactCodec.TryDecodeToken(request.PlanToken, out var parsedToken))
            {
                failure = CreateInvalidTokenFailure("Plan token format is invalid.");
                return false;
            }

            if (!PlanTokenCompactCodec.IsSupported(parsedToken))
            {
                failure = CreateInvalidTokenFailure("Plan token header values are not supported.");
                return false;
            }

            if (!PlanTokenKeyStore.TryLoadOrCreate(snapshot, out var signingKey, out var keyErrorMessage))
            {
                failure = new OperationFailure(
                    Code: IpcErrorCodes.InternalError,
                    Message: keyErrorMessage ?? "Failed to load plan-token signing key.",
                    OpId: null);
                return false;
            }

            if (!PlanTokenCompactCodec.VerifySignature(parsedToken, signingKey))
            {
                failure = CreateInvalidTokenFailure("Plan token signature is invalid.");
                return false;
            }

            var payload = parsedToken.Payload;
            if (!string.Equals(payload.ProjectFingerprint, snapshot.ProjectFingerprint, StringComparison.Ordinal))
            {
                failure = CreateInvalidTokenFailure("Plan token project fingerprint does not match current project.");
                return false;
            }

            var nowUtc = environment.UtcNow;
            if (nowUtc > payload.ExpiresAtUtc.Add(ClockSkew))
            {
                failure = new OperationFailure(
                    Code: IpcErrorCodes.PlanTokenExpired,
                    Message: "Plan token has expired.",
                    OpId: null);
                return false;
            }

            if (nowUtc < payload.IssuedAtUtc.Subtract(ClockSkew))
            {
                failure = CreateInvalidTokenFailure("Plan token issued-at timestamp is in the future.");
                return false;
            }

            var requestDigest = Sha256LowerHex.Compute(request.CanonicalDigestPayloadUtf8.Span);
            if (!string.Equals(requestDigest, payload.RequestDigest, StringComparison.Ordinal))
            {
                failure = new OperationFailure(
                    Code: IpcErrorCodes.PlanTokenRequestMismatch,
                    Message: "Plan token request digest does not match current request.",
                    OpId: null);
                return false;
            }

            decodedToken = parsedToken;
            return true;
        }

        /// <summary> Creates one invalid-token failure entry. </summary>
        /// <param name="message"> The failure message. </param>
        /// <returns> The failure entry. </returns>
        private static OperationFailure CreateInvalidTokenFailure (string message)
        {
            return new OperationFailure(
                Code: IpcErrorCodes.PlanTokenInvalid,
                Message: message,
                OpId: null);
        }
    }
}
