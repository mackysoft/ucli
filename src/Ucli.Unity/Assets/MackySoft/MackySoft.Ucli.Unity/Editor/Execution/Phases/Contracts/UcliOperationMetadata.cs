using System;
using System.Text.Json;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one operation metadata definition. </summary>
    public sealed class UcliOperationMetadata
    {
        private readonly UcliOperationDescribeIndexContractSnapshot contractSnapshot;

        private readonly ICallResultCompletion callResultCompletion;

        private UcliOperationMetadata (
            string operationName,
            UcliOperationKind kind,
            UcliOperationDescribeContract describeContract,
            Type argsType,
            Type resultType,
            ICallResultCompletion callResultCompletion,
            bool requiresPreCallPlanReplay,
            UcliOperationExposure exposure,
            UcliOperationPlayModeSupport playModeSupport)
        {
            ValidateOperationName(operationName);
            ValidateContractTypes(argsType, resultType);
            ValidateVocabularyValues(exposure, playModeSupport);
            var policy = ValidateDescribeContractAndDerivePolicy(
                operationName,
                kind,
                describeContract,
                exposure);
            ValidateResultContract(resultType, describeContract);

            OperationName = operationName;
            Kind = kind;
            Policy = policy;
            ArgsType = argsType;
            ResultType = resultType;
            this.callResultCompletion = callResultCompletion
                ?? throw new ArgumentNullException(nameof(callResultCompletion));
            RequiresPreCallPlanReplay = requiresPreCallPlanReplay;
            Exposure = exposure;
            PlayModeSupport = playModeSupport;
            contractSnapshot = UcliOperationDescribeIndexContractSnapshot.Create(
                operationName,
                kind,
                policy,
                describeContract,
                exposure,
                playModeSupport);
        }

        private static void ValidateOperationName (string operationName)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException(
                    "Operation name must not be null, empty, or whitespace.",
                    nameof(operationName));
            }

            if (StringValueValidator.HasOuterWhitespace(operationName))
            {
                throw new ArgumentException(
                    "Operation name must not contain leading or trailing whitespace.",
                    nameof(operationName));
            }
        }

        private static void ValidateContractTypes (Type argsType, Type resultType)
        {
            if (argsType == null)
            {
                throw new ArgumentNullException(nameof(argsType));
            }

            if (resultType == null)
            {
                throw new ArgumentNullException(nameof(resultType));
            }
        }

        private static void ValidateVocabularyValues (
            UcliOperationExposure exposure,
            UcliOperationPlayModeSupport playModeSupport)
        {
            if (!Vocabulary.IsDefined(exposure))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(exposure),
                    exposure,
                    "Operation exposure must be a defined value.");
            }

            if (!Vocabulary.IsDefined(playModeSupport))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playModeSupport),
                    playModeSupport,
                    "Operation Play Mode support must be a defined value.");
            }
        }

        private static OperationPolicy ValidateDescribeContractAndDerivePolicy (
            string operationName,
            UcliOperationKind kind,
            UcliOperationDescribeContract describeContract,
            UcliOperationExposure exposure)
        {
            if (describeContract == null)
            {
                throw new ArgumentNullException(nameof(describeContract));
            }

            if (!UcliOperationDescribeContractValidator.TryValidateRegisteredOperationDescribeContractAndDerivePolicy(
                    describeContract,
                    kind,
                    $"Describe contract for operation '{operationName}'",
                    exposure,
                    out var policy,
                    out var describeError))
            {
                throw new ArgumentException(describeError, nameof(describeContract));
            }

            return policy;
        }

        private static void ValidateResultContract (
            Type resultType,
            UcliOperationDescribeContract describeContract)
        {
            if ((resultType == typeof(UcliNoResult)) != (describeContract.ResultContract == null))
            {
                throw new ArgumentException(
                    "The generated result contract must be null exactly when the operation declares UcliNoResult.",
                    nameof(describeContract));
            }
        }

        /// <summary>
        /// Creates non-judging operation metadata from actual args and result serializer contracts.
        /// </summary>
        /// <typeparam name="TArgs"> The operation args DTO type. </typeparam>
        /// <typeparam name="TResult"> The operation result DTO type, or <see cref="UcliNoResult" />. </typeparam>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="kind"> The operation kind metadata. </param>
        /// <param name="description"> The operation purpose description. </param>
        /// <param name="assurance"> The agent-facing assurance metadata. </param>
        /// <param name="requiresPreCallPlanReplay"> Whether call execution must replay plan immediately beforehand. </param>
        /// <param name="exposure"> Whether the operation is reachable from public request surfaces. </param>
        /// <param name="playModeSupport"> Whether the raw operation can be executed through Play Mode mutation requests. </param>
        /// <param name="codeContract"> The optional source-facing code contract. </param>
        /// <returns> The created non-judging operation metadata. </returns>
        public static UcliOperationMetadata CreateWithoutVerdict<TArgs, TResult> (
            string operationName,
            UcliOperationKind kind,
            string description,
            UcliOperationAssuranceContract assurance,
            bool requiresPreCallPlanReplay,
            UcliOperationExposure exposure,
            UcliOperationPlayModeSupport playModeSupport,
            UcliOperationCodeContract? codeContract)
        {
            var generatedDescribeContract = UcliOperationJsonContractFactory.CreateWithoutVerdict<TArgs, TResult>(
                operationName,
                description,
                assurance,
                codeContract);
            return new UcliOperationMetadata(
                operationName,
                kind,
                generatedDescribeContract,
                typeof(TArgs),
                typeof(TResult),
                NonJudgingCallResultCompletion<TResult>.Instance,
                requiresPreCallPlanReplay,
                exposure,
                playModeSupport);
        }

        /// <summary>
        /// Creates typed metadata for a query that judges one condition from its successful Call result.
        /// </summary>
        /// <typeparam name="TArgs"> The operation args DTO type. </typeparam>
        /// <typeparam name="TResult"> The operation result DTO type. </typeparam>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="description"> The operation purpose description. </param>
        /// <param name="assurance"> The agent-facing assurance metadata. </param>
        /// <param name="verdict"> The typed condition evaluated from a successful Call result. </param>
        /// <param name="requiresPreCallPlanReplay"> Whether call execution must replay plan immediately beforehand. </param>
        /// <param name="exposure"> Whether the operation is reachable from public request surfaces. </param>
        /// <param name="playModeSupport"> Whether the raw operation can be executed through Play Mode mutation requests. </param>
        /// <param name="codeContract"> The optional source-facing code contract. </param>
        /// <returns> The created judging-query metadata. </returns>
        /// <exception cref="ArgumentNullException"> <paramref name="verdict" /> is <see langword="null" />. </exception>
        public static UcliOperationMetadata CreateJudgingQuery<TArgs, TResult> (
            string operationName,
            string description,
            UcliOperationAssuranceContract assurance,
            UcliOperationVerdictDefinition<TResult> verdict,
            bool requiresPreCallPlanReplay,
            UcliOperationExposure exposure,
            UcliOperationPlayModeSupport playModeSupport,
            UcliOperationCodeContract? codeContract)
        {
            if (verdict == null)
            {
                throw new ArgumentNullException(nameof(verdict));
            }

            var generatedDescribeContract = UcliOperationJsonContractFactory.CreateJudging<TArgs, TResult>(
                operationName,
                description,
                verdict.Contract,
                assurance,
                codeContract);
            return new UcliOperationMetadata(
                operationName,
                UcliOperationKind.Query,
                generatedDescribeContract,
                typeof(TArgs),
                typeof(TResult),
                new JudgingCallResultCompletion<TResult>(verdict),
                requiresPreCallPlanReplay,
                exposure,
                playModeSupport);
        }

        /// <summary> Gets the registered operation name. </summary>
        public string OperationName { get; }

        /// <summary> Gets the operation behavior kind metadata. </summary>
        public UcliOperationKind Kind { get; }

        /// <summary> Gets the operation policy metadata. </summary>
        public OperationPolicy Policy { get; }

        /// <summary> Gets the generated agent-facing operation describe contract. </summary>
        public UcliOperationDescribeContract DescribeContract => contractSnapshot.DescribeContract;

        /// <summary> Gets the operation args contract type. </summary>
        public Type ArgsType { get; }

        /// <summary> Gets the operation result contract type. </summary>
        public Type ResultType { get; }

        /// <summary> Gets the stable digest of the complete semantic operation descriptor. </summary>
        public Sha256Digest DescriptorDigest => contractSnapshot.DescriptorDigest;

        /// <summary> Gets a value indicating whether call execution must replay plan immediately beforehand. </summary>
        public bool RequiresPreCallPlanReplay { get; }

        /// <summary> Gets whether the operation is reachable from public request surfaces. </summary>
        public UcliOperationExposure Exposure { get; }

        /// <summary> Gets whether the raw operation can be executed through Play Mode mutation requests. </summary>
        public UcliOperationPlayModeSupport PlayModeSupport { get; }

        internal IndexOpEntryJsonContract IndexContract => contractSnapshot.IndexContract;

        internal OperationPhaseStepResult CompleteCallResult (
            OperationPhaseStepResult stepResult)
        {
            if (stepResult == null)
            {
                throw new ArgumentNullException(nameof(stepResult));
            }

            if (stepResult.TypedResult == null)
            {
                throw new InvalidOperationException(
                    "The operation step does not contain a typed result.");
            }

            return callResultCompletion.Complete(
                stepResult,
                stepResult.TypedResult);
        }

        private interface ICallResultCompletion
        {
            OperationPhaseStepResult Complete (
                OperationPhaseStepResult stepResult,
                object result);
        }

        private sealed class NonJudgingCallResultCompletion<TResult> : ICallResultCompletion
        {
            public static readonly NonJudgingCallResultCompletion<TResult> Instance =
                new NonJudgingCallResultCompletion<TResult>();

            private NonJudgingCallResultCompletion ()
            {
            }

            public OperationPhaseStepResult Complete (
                OperationPhaseStepResult stepResult,
                object result)
            {
                if (result is not TResult typedResult)
                {
                    throw new InvalidOperationException(
                        "The operation result does not match the non-judging metadata result type.");
                }

                var serializedResult = IpcPayloadCodec.SerializePublicRawOperationResultToElement(
                    typedResult);
                return stepResult with
                {
                    Result = OperationPhaseStepResult.CloneResult(serializedResult),
                    Verdict = null,
                    TypedResult = null,
                };
            }
        }

        private sealed class JudgingCallResultCompletion<TResult> : ICallResultCompletion
        {
            private readonly UcliOperationVerdictDefinition<TResult> verdict;

            public JudgingCallResultCompletion (
                UcliOperationVerdictDefinition<TResult> verdict)
            {
                this.verdict = verdict
                    ?? throw new ArgumentNullException(nameof(verdict));
            }

            public OperationPhaseStepResult Complete (
                OperationPhaseStepResult stepResult,
                object result)
            {
                if (result is not TResult typedResult)
                {
                    throw new InvalidOperationException(
                        "The operation result does not match the judging metadata result type.");
                }

                var evaluatedVerdict = verdict.Evaluate(typedResult);
                var serializedResult = IpcPayloadCodec.SerializePublicRawOperationResultToElement(
                    typedResult);
                return stepResult with
                {
                    Result = OperationPhaseStepResult.CloneResult(serializedResult),
                    Verdict = evaluatedVerdict,
                    TypedResult = null,
                };
            }
        }
    }
}
