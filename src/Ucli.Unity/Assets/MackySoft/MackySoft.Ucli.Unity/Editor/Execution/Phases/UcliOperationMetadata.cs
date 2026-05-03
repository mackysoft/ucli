using System;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Unity.Execution.Phases
{
    /// <summary> Represents one operation metadata definition. </summary>
    public class UcliOperationMetadata
    {
        /// <summary> Initializes a new instance of the <see cref="UcliOperationMetadata" /> class. </summary>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="kind"> The operation kind metadata. </param>
        /// <param name="policy"> The operation policy metadata. </param>
        /// <param name="describeContract"> The agent-facing operation describe contract. </param>
        /// <exception cref="ArgumentException"> Thrown when <paramref name="operationName" /> is invalid. </exception>
        public UcliOperationMetadata (
            string operationName,
            UcliOperationKind kind,
            OperationPolicy policy,
            UcliOperationDescribeContract describeContract)
            : this(
                operationName,
                kind,
                policy,
                describeContract,
                typeof(UcliEmptyArgs),
                typeof(UcliNoResult),
                requiresPreCallPlanReplay: false)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UcliOperationMetadata" /> class. </summary>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="kind"> The operation kind metadata. </param>
        /// <param name="policy"> The operation policy metadata. </param>
        /// <param name="describeContract"> The agent-facing operation describe contract. </param>
        /// <param name="argsType"> The operation args contract type. </param>
        /// <param name="resultType"> The operation result contract type. </param>
        /// <exception cref="ArgumentException"> Thrown when one argument is invalid. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when one contract type is <see langword="null" />. </exception>
        public UcliOperationMetadata (
            string operationName,
            UcliOperationKind kind,
            OperationPolicy policy,
            UcliOperationDescribeContract describeContract,
            Type argsType,
            Type resultType)
            : this(operationName, kind, policy, describeContract, argsType, resultType, requiresPreCallPlanReplay: false)
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UcliOperationMetadata" /> class. </summary>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="kind"> The operation kind metadata. </param>
        /// <param name="policy"> The operation policy metadata. </param>
        /// <param name="describeContract"> The agent-facing operation describe contract. </param>
        /// <param name="argsType"> The operation args contract type. </param>
        /// <param name="resultType"> The operation result contract type. </param>
        /// <param name="requiresPreCallPlanReplay"> Whether call execution must replay plan immediately beforehand. </param>
        /// <exception cref="ArgumentException"> Thrown when one argument is invalid. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when one contract type is <see langword="null" />. </exception>
        public UcliOperationMetadata (
            string operationName,
            UcliOperationKind kind,
            OperationPolicy policy,
            UcliOperationDescribeContract describeContract,
            Type argsType,
            Type resultType,
            bool requiresPreCallPlanReplay)
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

            ValidateDescribeContract(describeContract, resultType);

            OperationName = operationName;
            Kind = kind;
            Policy = policy;
            DescribeContract = describeContract;
            ArgsType = argsType;
            ResultType = resultType;
            ArgsSchemaJson = argsSchemaJson;
            ResultSchemaJson = resultSchemaJson;
            RequiresPreCallPlanReplay = requiresPreCallPlanReplay;
        }

        /// <summary> Creates typed operation metadata from args and result contract types. </summary>
        /// <typeparam name="TArgs"> The operation args contract type. </typeparam>
        /// <typeparam name="TResult"> The operation result contract type. </typeparam>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="kind"> The operation kind metadata. </param>
        /// <param name="policy"> The operation policy metadata. </param>
        /// <param name="describeContract"> The agent-facing operation describe contract. </param>
        /// <param name="requiresPreCallPlanReplay"> Whether call execution must replay plan immediately beforehand. </param>
        /// <returns> The created operation metadata. </returns>
        public static UcliOperationMetadata Create<TArgs, TResult> (
            string operationName,
            UcliOperationKind kind,
            OperationPolicy policy,
            UcliOperationDescribeContract describeContract,
            bool requiresPreCallPlanReplay = false)
        {
            return new UcliOperationMetadata(
                operationName,
                kind,
                policy,
                describeContract,
                typeof(TArgs),
                typeof(TResult),
                requiresPreCallPlanReplay);
        }

        /// <summary> Creates typed operation metadata and derives the input contract from args attributes. </summary>
        /// <typeparam name="TArgs"> The operation args contract type. </typeparam>
        /// <typeparam name="TResult"> The operation result contract type. </typeparam>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="kind"> The operation kind metadata. </param>
        /// <param name="policy"> The operation policy metadata. </param>
        /// <param name="description"> The operation purpose description. </param>
        /// <param name="assurance"> The agent-facing assurance metadata. </param>
        /// <param name="requiresPreCallPlanReplay"> Whether call execution must replay plan immediately beforehand. </param>
        /// <returns> The created operation metadata. </returns>
        public static UcliOperationMetadata Create<TArgs, TResult> (
            string operationName,
            UcliOperationKind kind,
            OperationPolicy policy,
            string description,
            UcliOperationAssuranceContract assurance,
            bool requiresPreCallPlanReplay = false)
        {
            return Create<TArgs, TResult>(
                operationName,
                kind,
                policy,
                UcliOperationDescribeContractBuilder.Create<TArgs, TResult>(description, assurance),
                requiresPreCallPlanReplay);
        }

        /// <summary> Gets the registered operation name. </summary>
        public string OperationName { get; }

        /// <summary> Gets the operation behavior kind metadata. </summary>
        public UcliOperationKind Kind { get; }

        /// <summary> Gets the operation policy metadata. </summary>
        public OperationPolicy Policy { get; }

        /// <summary> Gets the agent-facing operation describe contract. </summary>
        public UcliOperationDescribeContract DescribeContract { get; }

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

        private static void ValidateDescribeContract (
            UcliOperationDescribeContract describeContract,
            Type resultType)
        {
            if (string.IsNullOrWhiteSpace(describeContract.Description)
                || describeContract.Inputs == null
                || describeContract.ResultContract == null
                || describeContract.Assurance == null)
            {
                throw new ArgumentException("Describe contract must include description, inputs, resultContract, and assurance.", nameof(describeContract));
            }

            if (describeContract.Assurance.SideEffects == null
                || describeContract.Assurance.TouchedKinds == null
                || string.IsNullOrWhiteSpace(describeContract.Assurance.PlanMode))
            {
                throw new ArgumentException("Describe contract assurance fields must be complete.", nameof(describeContract));
            }

            if (resultType == typeof(UcliNoResult))
            {
                ValidateNoResultContract(describeContract.ResultContract);
                return;
            }

            ValidateEmittedResultContract(describeContract.ResultContract, resultType);
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
