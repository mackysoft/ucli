using System;
using System.Collections.Generic;
using System.Threading;
using MackySoft.Ucli.Contracts.Configuration;
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

        /// <summary> Issues one plan token from normalized request and plan traces. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="operationTraces"> The plan-phase operation traces. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The token issue result. </returns>
        public PlanTokenIssueResult Issue (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
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
                var requestDigest = ComputeRequestDigest(request);
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

        /// <summary> Validates one incoming call plan token against request and current state. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <param name="operationTraces"> The pre-call plan traces. </param>
        /// <param name="cancellationToken"> The cancellation token propagated by phase execution. </param>
        /// <returns> The validation result. </returns>
        public PlanTokenValidationResult ValidateCall (
            NormalizedExecuteRequest request,
            IReadOnlyList<OperationPhaseTrace> operationTraces,
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
                var config = PlanTokenConfigResolver.Resolve(snapshot.RepositoryRoot);
                if (string.IsNullOrWhiteSpace(request.PlanToken))
                {
                    if (config.Mode == PlanTokenMode.Required)
                    {
                        return PlanTokenValidationResult.Failed(new OperationFailure(
                            Code: IpcErrorCodes.PlanTokenRequired,
                            Message: "Plan token is required for call execution.",
                            OpId: null));
                    }

                    return PlanTokenValidationResult.Success();
                }

                if (!PlanTokenCompactCodec.TryDecodeToken(request.PlanToken, out var decodedToken))
                {
                    return PlanTokenValidationResult.Failed(CreateInvalidTokenFailure("Plan token format is invalid."));
                }

                if (!PlanTokenCompactCodec.IsSupported(decodedToken))
                {
                    return PlanTokenValidationResult.Failed(CreateInvalidTokenFailure("Plan token header values are not supported."));
                }

                if (!PlanTokenKeyStore.TryLoadOrCreate(snapshot, out var signingKey, out var keyErrorMessage))
                {
                    return PlanTokenValidationResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.InternalError,
                        Message: keyErrorMessage ?? "Failed to load plan-token signing key.",
                        OpId: null));
                }

                if (!PlanTokenCompactCodec.VerifySignature(decodedToken, signingKey))
                {
                    return PlanTokenValidationResult.Failed(CreateInvalidTokenFailure("Plan token signature is invalid."));
                }

                var payload = decodedToken.Payload;
                if (!string.Equals(payload.ProjectFingerprint, snapshot.ProjectFingerprint, StringComparison.Ordinal))
                {
                    return PlanTokenValidationResult.Failed(CreateInvalidTokenFailure("Plan token project fingerprint does not match current project."));
                }

                var nowUtc = environment.UtcNow;
                if (nowUtc > payload.ExpiresAtUtc.Add(ClockSkew))
                {
                    return PlanTokenValidationResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.PlanTokenExpired,
                        Message: "Plan token has expired.",
                        OpId: null));
                }

                if (nowUtc < payload.IssuedAtUtc.Subtract(ClockSkew))
                {
                    return PlanTokenValidationResult.Failed(CreateInvalidTokenFailure("Plan token issued-at timestamp is in the future."));
                }

                var requestDigest = ComputeRequestDigest(request);
                if (!string.Equals(requestDigest, payload.RequestDigest, StringComparison.Ordinal))
                {
                    return PlanTokenValidationResult.Failed(new OperationFailure(
                        Code: IpcErrorCodes.PlanTokenRequestMismatch,
                        Message: "Plan token request digest does not match current request.",
                        OpId: null));
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

        /// <summary> Computes deterministic request digest from normalized request canonical payload. </summary>
        /// <param name="request"> The normalized request model. </param>
        /// <returns> The lowercase hexadecimal digest string. </returns>
        private static string ComputeRequestDigest (NormalizedExecuteRequest request)
        {
            return PlanTokenSha256Hex.Compute(request.CanonicalDigestPayloadUtf8.Span);
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
