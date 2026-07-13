using System;
using System.Collections.Generic;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one operation metadata definition. </summary>
    public class UcliOperationMetadata
    {
        private readonly UcliOperationDescribeContract describeContract;

        /// <summary> Initializes a new instance of the <see cref="UcliOperationMetadata" /> class. </summary>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="kind"> The operation kind metadata. </param>
        /// <param name="describeContract"> The agent-facing operation describe contract. </param>
        /// <param name="exposure"> Whether the operation is reachable from public request surfaces. </param>
        /// <param name="playModeSupport"> Whether the raw operation can be executed through Play Mode mutation requests. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="operationName" /> is invalid. </exception>
        public UcliOperationMetadata (
            string operationName,
            UcliOperationKind kind,
            UcliOperationDescribeContract describeContract,
            UcliOperationExposure exposure = UcliOperationExposure.Public,
            UcliOperationPlayModeSupport playModeSupport = UcliOperationPlayModeSupport.Disallowed)
            : this(
                operationName,
                kind,
                describeContract,
                typeof(UcliEmptyArgs),
                typeof(UcliNoResult),
                requiresPreCallPlanReplay: false,
                exposure: exposure,
                playModeSupport: playModeSupport)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UcliOperationMetadata" /> class. </summary>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="kind"> The operation kind metadata. </param>
        /// <param name="describeContract"> The agent-facing operation describe contract. </param>
        /// <param name="argsType"> The operation args contract type. </param>
        /// <param name="resultType"> The operation result contract type. </param>
        /// <param name="requiresPreCallPlanReplay"> Whether call execution must replay plan immediately beforehand. </param>
        /// <param name="exposure"> Whether the operation is reachable from public request surfaces. </param>
        /// <param name="playModeSupport"> Whether the raw operation can be executed through Play Mode mutation requests. </param>
        /// <exception cref="ArgumentException"> Thrown when one argument is invalid. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when one contract type is <see langword="null" />. </exception>
        public UcliOperationMetadata (
            string operationName,
            UcliOperationKind kind,
            UcliOperationDescribeContract describeContract,
            Type argsType,
            Type resultType,
            bool requiresPreCallPlanReplay,
            UcliOperationExposure exposure = UcliOperationExposure.Public,
            UcliOperationPlayModeSupport playModeSupport = UcliOperationPlayModeSupport.Disallowed)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("Operation name must not be null, empty, or whitespace.", nameof(operationName));
            }

            if (StringValueValidator.HasOuterWhitespace(operationName))
            {
                throw new ArgumentException("Operation name must not contain leading or trailing whitespace.", nameof(operationName));
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

            if (!ContractLiteralCodec.IsDefined(playModeSupport))
            {
                throw new ArgumentOutOfRangeException(nameof(playModeSupport), playModeSupport, "Operation Play Mode support must be a defined value.");
            }

            if (!UcliOperationContractValidator.TryValidatePublicRawOpReservedProperties(argsType, out var reservedPropertyError))
            {
                throw new ArgumentException(reservedPropertyError, nameof(argsType));
            }

            var argsSchemaJson = UcliOperationJsonSchemaGenerator.CreateArgsSchemaJson(argsType);
            var resultSchemaJson = UcliOperationJsonSchemaGenerator.CreateResultSchemaJson(resultType);

            if (string.IsNullOrWhiteSpace(argsSchemaJson))
            {
                throw new ArgumentException("Args schema JSON must not be null, empty, or whitespace.", nameof(argsSchemaJson));
            }

            ValidateSchemaJson(argsSchemaJson, nameof(argsSchemaJson), "Args schema JSON");
            if (resultSchemaJson != null)
            {
                ValidateSchemaJson(resultSchemaJson, nameof(resultSchemaJson), "Result schema JSON");
            }

            var policy = ValidateDescribeContract(operationName, kind, describeContract, resultType, exposure);
            var ownedDescribeContract = CopyDescribeContract(describeContract);

            OperationName = operationName;
            Kind = kind;
            Policy = policy;
            this.describeContract = ownedDescribeContract;
            ArgsType = argsType;
            ResultType = resultType;
            ArgsSchemaJson = argsSchemaJson;
            ResultSchemaJson = resultSchemaJson;
            RequiresPreCallPlanReplay = requiresPreCallPlanReplay;
            Exposure = exposure;
            PlayModeSupport = playModeSupport;
        }

        /// <summary> Creates typed operation metadata from args and result contract types. </summary>
        /// <typeparam name="TArgs"> The operation args contract type. </typeparam>
        /// <typeparam name="TResult"> The operation result contract type. </typeparam>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="kind"> The operation kind metadata. </param>
        /// <param name="describeContract"> The agent-facing operation describe contract. </param>
        /// <param name="requiresPreCallPlanReplay"> Whether call execution must replay plan immediately beforehand. </param>
        /// <param name="exposure"> Whether the operation is reachable from public request surfaces. </param>
        /// <param name="playModeSupport"> Whether the raw operation can be executed through Play Mode mutation requests. </param>
        /// <returns> The created operation metadata. </returns>
        public static UcliOperationMetadata Create<TArgs, TResult> (
            string operationName,
            UcliOperationKind kind,
            UcliOperationDescribeContract describeContract,
            bool requiresPreCallPlanReplay = false,
            UcliOperationExposure exposure = UcliOperationExposure.Public,
            UcliOperationPlayModeSupport playModeSupport = UcliOperationPlayModeSupport.Disallowed)
        {
            return new UcliOperationMetadata(
                operationName,
                kind,
                describeContract,
                typeof(TArgs),
                typeof(TResult),
                requiresPreCallPlanReplay,
                exposure,
                playModeSupport);
        }

        /// <summary> Creates typed operation metadata and derives the input contract from args attributes. </summary>
        /// <typeparam name="TArgs"> The operation args contract type. </typeparam>
        /// <typeparam name="TResult"> The operation result contract type. </typeparam>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="kind"> The operation kind metadata. </param>
        /// <param name="description"> The operation purpose description. </param>
        /// <param name="assurance"> The agent-facing assurance metadata. </param>
        /// <param name="requiresPreCallPlanReplay"> Whether call execution must replay plan immediately beforehand. </param>
        /// <param name="exposure"> Whether the operation is reachable from public request surfaces. </param>
        /// <param name="playModeSupport"> Whether the raw operation can be executed through Play Mode mutation requests. </param>
        /// <returns> The created operation metadata. </returns>
        public static UcliOperationMetadata Create<TArgs, TResult> (
            string operationName,
            UcliOperationKind kind,
            string description,
            UcliOperationAssuranceContract assurance,
            bool requiresPreCallPlanReplay = false,
            UcliOperationExposure exposure = UcliOperationExposure.Public,
            UcliOperationPlayModeSupport playModeSupport = UcliOperationPlayModeSupport.Disallowed)
        {
            return Create<TArgs, TResult>(
                operationName,
                kind,
                UcliOperationDescribeContractBuilder.Create<TArgs, TResult>(description, assurance),
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

        /// <summary> Gets the agent-facing operation describe contract. </summary>
        public UcliOperationDescribeContract DescribeContract => CopyDescribeContract(describeContract);

        /// <summary> Gets the operation args contract type. </summary>
        public Type ArgsType { get; }

        /// <summary> Gets the operation result contract type. </summary>
        public Type ResultType { get; }

        /// <summary> Gets the args-schema JSON object text. </summary>
        public string ArgsSchemaJson { get; }

        /// <summary> Gets the result-schema JSON object text, or <see langword="null" /> when no result payload is emitted. </summary>
        public string? ResultSchemaJson { get; }

        /// <summary> Gets a value indicating whether call execution must replay plan immediately beforehand. </summary>
        public bool RequiresPreCallPlanReplay { get; }

        /// <summary> Gets whether the operation is reachable from public request surfaces. </summary>
        public UcliOperationExposure Exposure { get; }

        /// <summary> Gets whether the raw operation can be executed through Play Mode mutation requests. </summary>
        public UcliOperationPlayModeSupport PlayModeSupport { get; }

        private static OperationPolicy ValidateDescribeContract (
            string operationName,
            UcliOperationKind kind,
            UcliOperationDescribeContract describeContract,
            Type resultType,
            UcliOperationExposure exposure)
        {
            if (!UcliOperationDescribeContractValidator.TryValidateRegisteredOperationDescribeContractAndDerivePolicy(
                    describeContract,
                    ContractLiteralCodec.ToValue(kind),
                    $"Describe contract for operation '{operationName}'",
                    exposure,
                    out var policy,
                    out var describeInputError))
            {
                throw new ArgumentException(describeInputError, nameof(describeContract));
            }

            if (resultType == typeof(UcliNoResult))
            {
                ValidateNoResultContract(describeContract.ResultContract);
                return policy;
            }

            ValidateEmittedResultContract(describeContract.ResultContract, resultType);
            return policy;
        }

        private static UcliOperationDescribeContract CopyDescribeContract (UcliOperationDescribeContract source)
        {
            return new UcliOperationDescribeContract(
                source.Description,
                CopyInputs(source.Inputs),
                CopyResultContract(source.ResultContract),
                CopyAssurance(source.Assurance),
                CopyCodeContract(source.CodeContract));
        }

        private static IReadOnlyList<UcliOperationInputContract>? CopyInputs (IReadOnlyList<UcliOperationInputContract>? source)
        {
            if (source == null)
            {
                return null;
            }

            var inputs = new UcliOperationInputContract[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                inputs[i] = CopyInput(source[i]);
            }

            return inputs;
        }

        private static UcliOperationInputContract CopyInput (UcliOperationInputContract source)
        {
            return new UcliOperationInputContract(
                source.Name,
                source.ValueType,
                source.Description,
                CopyConstraints(source.Constraints),
                source.ArgsPath,
                CopyVariants(source.Variants));
        }

        private static IReadOnlyList<UcliOperationInputVariantContract>? CopyVariants (IReadOnlyList<UcliOperationInputVariantContract>? source)
        {
            if (source == null)
            {
                return null;
            }

            var variants = new UcliOperationInputVariantContract[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                variants[i] = new UcliOperationInputVariantContract(
                    source[i].Name,
                    source[i].Description,
                    CopyVariantFields(source[i].Fields));
            }

            return variants;
        }

        private static IReadOnlyList<UcliOperationInputVariantFieldContract>? CopyVariantFields (IReadOnlyList<UcliOperationInputVariantFieldContract>? source)
        {
            if (source == null)
            {
                return null;
            }

            var fields = new UcliOperationInputVariantFieldContract[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                fields[i] = new UcliOperationInputVariantFieldContract(
                    source[i].Name,
                    source[i].ArgsPath,
                    source[i].Description,
                    CopyConstraints(source[i].Constraints));
            }

            return fields;
        }

        private static IReadOnlyList<UcliOperationInputConstraintContract>? CopyConstraints (IReadOnlyList<UcliOperationInputConstraintContract>? source)
        {
            if (source == null)
            {
                return null;
            }

            var constraints = new UcliOperationInputConstraintContract[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                constraints[i] = new UcliOperationInputConstraintContract(source[i].Kind)
                {
                    Access = source[i].Access,
                    AssetKind = source[i].AssetKind,
                    Max = source[i].Max,
                    Min = source[i].Min,
                    TargetKind = source[i].TargetKind,
                    TypeKind = source[i].TypeKind,
                };
            }

            return constraints;
        }

        private static UcliOperationResultContract? CopyResultContract (UcliOperationResultContract? source)
        {
            if (source == null)
            {
                return null;
            }

            return new UcliOperationResultContract(
                source.Emitted,
                source.ResultType,
                source.Description);
        }

        private static UcliOperationAssuranceContract? CopyAssurance (UcliOperationAssuranceContract? source)
        {
            if (source == null)
            {
                return null;
            }

            return new UcliOperationAssuranceContract(
                CopyStrings(source.SideEffects),
                CopyStrings(source.TouchedKinds),
                source.PlanMode,
                source.PlanSemantics,
                source.CallSemantics,
                source.TouchedContract,
                source.ReadPostconditionContract,
                source.FailureSemantics,
                CopyStrings(source.DangerousNotes));
        }

        private static UcliOperationCodeContract? CopyCodeContract (UcliOperationCodeContract? source)
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

        private static UcliCodeEntryPointContract? CopyCodeEntryPoint (UcliCodeEntryPointContract? source)
        {
            if (source == null)
            {
                return null;
            }

            return new UcliCodeEntryPointContract(
                source.Signature,
                source.MatchRule,
                source.RequiredStatic,
                CopyStrings(source.ParameterTypes),
                source.ReturnValue);
        }

        private static IReadOnlyList<UcliCodeSourceFormContract>? CopyCodeSourceForms (IReadOnlyList<UcliCodeSourceFormContract>? source)
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

        private static IReadOnlyList<UcliCodeApiTypeContract>? CopyCodeApiTypes (IReadOnlyList<UcliCodeApiTypeContract>? source)
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

        private static IReadOnlyList<UcliCodeApiMemberContract>? CopyCodeApiMembers (IReadOnlyList<UcliCodeApiMemberContract>? source)
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

        private static IReadOnlyList<UcliCodeApiParameterContract>? CopyCodeApiParameters (IReadOnlyList<UcliCodeApiParameterContract>? source)
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

        private static IReadOnlyList<string>? CopyStrings (IReadOnlyList<string>? source)
        {
            if (source == null)
            {
                return null;
            }

            var values = new string[source.Count];
            for (var i = 0; i < source.Count; i++)
            {
                values[i] = source[i];
            }

            return values;
        }

        private static void ValidateNoResultContract (UcliOperationResultContract resultContract)
        {
            if (resultContract.Emitted
                || !string.Equals(resultContract.ResultType, nameof(UcliNoResult), StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(resultContract.Description))
            {
                throw new ArgumentException("No-result operations must declare a matching resultContract.", nameof(resultContract));
            }
        }

        private static void ValidateEmittedResultContract (
            UcliOperationResultContract resultContract,
            Type resultType)
        {
            if (!resultContract.Emitted
                || string.IsNullOrWhiteSpace(resultContract.ResultType)
                || !string.Equals(resultContract.ResultType, resultType.Name, StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(resultContract.Description))
            {
                throw new ArgumentException("Result-emitting operations must declare a matching resultContract.", nameof(resultContract));
            }
        }

        private static void ValidateSchemaJson (
            string schemaJson,
            string parameterName,
            string displayName)
        {
            try
            {
                using var document = JsonDocument.Parse(schemaJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException($"{displayName} must be a JSON object.", parameterName);
                }
            }
            catch (JsonException exception)
            {
                throw new ArgumentException($"{displayName} is invalid. {exception.Message}", parameterName, exception);
            }
        }
    }
}
