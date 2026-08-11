using System.Text;
using System.Text.Json.Serialization.Metadata;
using MackySoft.JsonSchema.Generation;
using MackySoft.JsonSchema.Generation.Diagnostics;
using MackySoft.JsonSchema.Generation.Projection;
using MackySoft.Ucli.Contracts.Cryptography;
using MackySoft.Ucli.Contracts.Json;
using MackySoft.Ucli.Contracts.Json.Generation;
using MackySoft.Ucli.Contracts.Operations;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary>
/// Generates the JSON Contract Models and projections assigned to one uCLI operation.
/// </summary>
public static class UcliOperationJsonContractGenerator
{
    private const string ContractIdPrefix = "ucli.operation/";

    private static readonly JsonSchemaDocumentOptions DocumentOptions = new(
        JsonSchemaDocumentKind.Fragment,
        id: null,
        logicalName: null);

    /// <summary>
    /// Generates the args contract and, when <paramref name="resultTypeInfo" /> is present, the result contract for
    /// one operation.
    /// </summary>
    /// <param name="operationName"> The exact public operation name used to derive stable contract identifiers. </param>
    /// <param name="argsTypeInfo">
    /// The effective serializer contract for the operation args.
    /// </param>
    /// <param name="resultTypeInfo">
    /// The effective serializer contract for the operation result, or <see langword="null" /> when the operation
    /// declares <see cref="UcliNoResult" />.
    /// </param>
    /// <returns> The provider generation results for the operation contracts. </returns>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="operationName" /> or <paramref name="argsTypeInfo" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="operationName" /> is empty, whitespace, has outer whitespace, or contains malformed UTF-16;
    /// or an args or result serializer contract does not produce a JSON object root.
    /// </exception>
    /// <exception cref="JsonContractGenerationException">
    /// An operation serializer contract cannot be interpreted without violating the provider generation contract.
    /// </exception>
    public static UcliOperationJsonContractGenerationResult Generate (
        string operationName,
        JsonTypeInfo argsTypeInfo,
        JsonTypeInfo? resultTypeInfo)
    {
        ValidateOperationName(operationName);
        if (argsTypeInfo == null)
        {
            throw new ArgumentNullException(nameof(argsTypeInfo));
        }

        var operationNameDigest = Sha256LowerHex.Compute(Encoding.UTF8.GetBytes(operationName));
        return new UcliOperationJsonContractGenerationResult(
            GenerateOperationContract(
                operationNameDigest,
                "args",
                argsTypeInfo,
                nameof(argsTypeInfo)),
            resultTypeInfo == null
                ? null
                : GenerateOperationContract(
                    operationNameDigest,
                    "result",
                    resultTypeInfo,
                    nameof(resultTypeInfo)));
    }

    private static JsonContractGenerationResult GenerateOperationContract (
        string operationNameDigest,
        string role,
        JsonTypeInfo typeInfo,
        string parameterName)
    {
        return GenerateContract(
            ContractIdPrefix + operationNameDigest + "/" + role,
            CreateNonNullObjectRootTypeInfo(typeInfo, parameterName));
    }

    private static JsonContractGenerationResult GenerateContract (
        string contractId,
        JsonTypeInfo typeInfo)
    {
        return UcliJsonContractGenerator.GenerateWithOperationContractProfile(
            contractId,
            typeInfo,
            DocumentOptions);
    }

    private static JsonTypeInfo CreateNonNullObjectRootTypeInfo (
        JsonTypeInfo typeInfo,
        string parameterName)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            throw new ArgumentException(
                $"Operation contract type '{typeInfo.Type.FullName}' must produce a JSON object root.",
                parameterName);
        }

        var valueType = UcliNonNullJsonObject.MakeValueType(typeInfo.Type);
        var valueTypeInfo = typeInfo.Options.GetTypeInfo(valueType);
        if (!UcliNonNullJsonObject.IsValueConverter(valueTypeInfo.Converter))
        {
            throw new ArgumentException(
                "Operation contract serializer options must include the uCLI non-null JSON object converter.",
                parameterName);
        }

        return valueTypeInfo;
    }

    private static void ValidateOperationName (string operationName)
    {
        if (operationName == null)
        {
            throw new ArgumentNullException(nameof(operationName));
        }

        if (string.IsNullOrWhiteSpace(operationName)
            || StringValueValidator.HasOuterWhitespace(operationName)
            || !StringValueValidator.IsWellFormedUtf16(operationName))
        {
            throw new ArgumentException(
                "Operation name must not be empty, whitespace, have outer whitespace, or contain malformed UTF-16.",
                nameof(operationName));
        }
    }
}
