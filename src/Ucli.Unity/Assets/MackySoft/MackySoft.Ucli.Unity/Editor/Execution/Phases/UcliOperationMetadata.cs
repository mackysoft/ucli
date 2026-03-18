using System;
using System.Text.Json;
using MackySoft.Ucli.Contracts.Configuration;
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
        /// <exception cref="ArgumentException"> Thrown when <paramref name="operationName" /> is invalid. </exception>
        public UcliOperationMetadata (
            string operationName,
            UcliOperationKind kind,
            OperationPolicy policy)
            : this(operationName, kind, policy, "{\"type\":\"object\"}")
        {
        }

        /// <summary> Initializes a new instance of the <see cref="UcliOperationMetadata" /> class. </summary>
        /// <param name="operationName"> The operation name. </param>
        /// <param name="kind"> The operation kind metadata. </param>
        /// <param name="policy"> The operation policy metadata. </param>
        /// <param name="argsSchemaJson"> The args-schema JSON object text. </param>
        /// <exception cref="ArgumentException"> Thrown when one argument is invalid. </exception>
        public UcliOperationMetadata (
            string operationName,
            UcliOperationKind kind,
            OperationPolicy policy,
            string argsSchemaJson)
        {
            if (string.IsNullOrWhiteSpace(operationName))
            {
                throw new ArgumentException("Operation name must not be null, empty, or whitespace.", nameof(operationName));
            }

            if (StringValueValidator.HasOuterWhitespace(operationName))
            {
                throw new ArgumentException("Operation name must not contain leading or trailing whitespace.", nameof(operationName));
            }

            if (string.IsNullOrWhiteSpace(argsSchemaJson))
            {
                throw new ArgumentException("Args schema JSON must not be null, empty, or whitespace.", nameof(argsSchemaJson));
            }

            try
            {
                using var document = JsonDocument.Parse(argsSchemaJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException("Args schema JSON must be a JSON object.", nameof(argsSchemaJson));
                }
            }
            catch (JsonException exception)
            {
                throw new ArgumentException($"Args schema JSON is invalid. {exception.Message}", nameof(argsSchemaJson), exception);
            }

            OperationName = operationName;
            Kind = kind;
            Policy = policy;
            ArgsSchemaJson = argsSchemaJson;
        }

        /// <summary> Gets the registered operation name. </summary>
        public string OperationName { get; }

        /// <summary> Gets the operation behavior kind metadata. </summary>
        public UcliOperationKind Kind { get; }

        /// <summary> Gets the operation policy metadata. </summary>
        public OperationPolicy Policy { get; }

        /// <summary> Gets the args-schema JSON object text. </summary>
        public string ArgsSchemaJson { get; }
    }
}