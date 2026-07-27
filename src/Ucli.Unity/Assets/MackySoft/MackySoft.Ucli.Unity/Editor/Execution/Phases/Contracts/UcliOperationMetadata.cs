using System;
using System.Collections.Generic;
using MackySoft.Text.Vocabularies;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one operation metadata definition. </summary>
    public sealed class UcliOperationMetadata
    {
        private readonly UcliOperationDescribeContract describeContract;

        private UcliOperationMetadata (
            string operationName,
            UcliOperationKind kind,
            UcliOperationDescribeContract describeContract,
            Type argsType,
            Type resultType,
            bool requiresPreCallPlanReplay,
            UcliOperationExposure exposure,
            UcliOperationPlayModeSupport playModeSupport)
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

            if (argsType == null)
            {
                throw new ArgumentNullException(nameof(argsType));
            }

            if (resultType == null)
            {
                throw new ArgumentNullException(nameof(resultType));
            }

            if (describeContract == null)
            {
                throw new ArgumentNullException(nameof(describeContract));
            }

            if (!Vocabulary.IsDefined(playModeSupport))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(playModeSupport),
                    playModeSupport,
                    "Operation Play Mode support must be a defined value.");
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

            if ((resultType == typeof(UcliNoResult)) != (describeContract.ResultContract == null))
            {
                throw new ArgumentException(
                    "The generated result contract must be null exactly when the operation declares UcliNoResult.",
                    nameof(describeContract));
            }

            OperationName = operationName;
            Kind = kind;
            Policy = policy;
            this.describeContract = CopyDescribeContract(describeContract);
            ArgsType = argsType;
            ResultType = resultType;
            RequiresPreCallPlanReplay = requiresPreCallPlanReplay;
            Exposure = exposure;
            PlayModeSupport = playModeSupport;
        }

        /// <summary>
        /// Creates typed operation metadata from actual args and result serializer contracts.
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
        /// <returns> The created operation metadata. </returns>
        public static UcliOperationMetadata Create<TArgs, TResult> (
            string operationName,
            UcliOperationKind kind,
            string description,
            UcliOperationAssuranceContract assurance,
            bool requiresPreCallPlanReplay = false,
            UcliOperationExposure exposure = UcliOperationExposure.Public,
            UcliOperationPlayModeSupport playModeSupport = UcliOperationPlayModeSupport.Disallowed,
            UcliOperationCodeContract? codeContract = null)
        {
            var generatedDescribeContract = UcliOperationJsonContractFactory.Create<TArgs, TResult>(
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
        public UcliOperationDescribeContract DescribeContract => CopyDescribeContract(describeContract);

        /// <summary> Gets the operation args contract type. </summary>
        public Type ArgsType { get; }

        /// <summary> Gets the operation result contract type. </summary>
        public Type ResultType { get; }

        /// <summary> Gets a value indicating whether call execution must replay plan immediately beforehand. </summary>
        public bool RequiresPreCallPlanReplay { get; }

        /// <summary> Gets whether the operation is reachable from public request surfaces. </summary>
        public UcliOperationExposure Exposure { get; }

        /// <summary> Gets whether the raw operation can be executed through Play Mode mutation requests. </summary>
        public UcliOperationPlayModeSupport PlayModeSupport { get; }

        private static UcliOperationDescribeContract CopyDescribeContract (
            UcliOperationDescribeContract source)
        {
            return new UcliOperationDescribeContract(
                source.Description,
                CopyGeneratedContract(source.ArgsContract),
                CopyGeneratedContract(source.ResultContract),
                source.Assurance,
                CopyCodeContract(source.CodeContract));
        }

        private static UcliOperationJsonContract? CopyGeneratedContract (
            UcliOperationJsonContract? source)
        {
            return source;
        }

        private static UcliOperationCodeContract? CopyCodeContract (
            UcliOperationCodeContract? source)
        {
            if (source == null)
            {
                return null;
            }

            return new UcliOperationCodeContract(
                source.Language,
                CopyCodeEntryPoint(source.EntryPoint),
                CopyCodeSourceForms(source.SourceForms),
                CopyCodeApiTypes(source.ApiTypes));
        }

        private static UcliCodeEntryPointContract? CopyCodeEntryPoint (
            UcliCodeEntryPointContract? source)
        {
            if (source == null)
            {
                return null;
            }

            return new UcliCodeEntryPointContract(
                source.Signature,
                source.MatchRule,
                source.RequiredStatic,
                CopyValues(source.ParameterTypes),
                source.ReturnValue);
        }

        private static IReadOnlyList<UcliCodeSourceFormContract>? CopyCodeSourceForms (
            IReadOnlyList<UcliCodeSourceFormContract>? source)
        {
            if (source == null)
            {
                return null;
            }

            var result = new UcliCodeSourceFormContract[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                result[i] = new UcliCodeSourceFormContract(
                    source[i].Kind,
                    source[i].Description);
            }

            return result;
        }

        private static IReadOnlyList<UcliCodeApiTypeContract>? CopyCodeApiTypes (
            IReadOnlyList<UcliCodeApiTypeContract>? source)
        {
            if (source == null)
            {
                return null;
            }

            var apiTypes = new UcliCodeApiTypeContract[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                apiTypes[i] = new UcliCodeApiTypeContract(
                    source[i].Name,
                    source[i].FullName,
                    source[i].Description,
                    CopyCodeApiMembers(source[i].Members));
            }

            return apiTypes;
        }

        private static IReadOnlyList<UcliCodeApiMemberContract>? CopyCodeApiMembers (
            IReadOnlyList<UcliCodeApiMemberContract>? source)
        {
            if (source == null)
            {
                return null;
            }

            var members = new UcliCodeApiMemberContract[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                members[i] = new UcliCodeApiMemberContract(
                    source[i].Kind,
                    source[i].Name,
                    source[i].Description,
                    source[i].Type,
                    source[i].ReturnType,
                    CopyCodeApiParameters(source[i].Parameters));
            }

            return members;
        }

        private static IReadOnlyList<UcliCodeApiParameterContract>? CopyCodeApiParameters (
            IReadOnlyList<UcliCodeApiParameterContract>? source)
        {
            if (source == null)
            {
                return null;
            }

            var parameters = new UcliCodeApiParameterContract[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                parameters[i] = new UcliCodeApiParameterContract(
                    source[i].Name,
                    source[i].Type,
                    source[i].Description);
            }

            return parameters;
        }

        private static IReadOnlyList<T>? CopyValues<T> (IReadOnlyList<T>? source)
        {
            if (source == null)
            {
                return null;
            }

            var values = new T[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                values[i] = source[i];
            }

            return values;
        }
    }
}
