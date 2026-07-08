using System.Text.Json;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;

/// <summary> Implements mapping from catalog snapshots to command-facing <c>ops describe</c> results. </summary>
internal sealed class OpsDescribeResultMapper : IOpsDescribeResultMapper
{
    private readonly OpsReadIndexInfoMapper readIndexInfoMapper;

    /// <summary> Initializes a new instance of the <see cref="OpsDescribeResultMapper" /> class. </summary>
    /// <param name="readIndexInfoMapper"> The read-index info mapper dependency. </param>
    public OpsDescribeResultMapper (OpsReadIndexInfoMapper readIndexInfoMapper)
    {
        this.readIndexInfoMapper = readIndexInfoMapper ?? throw new ArgumentNullException(nameof(readIndexInfoMapper));
    }

    /// <inheritdoc />
    public OpsDescribeServiceResult Map (OpsDescribeReadOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        var operation = output.Operation;
        var operationName = operation.Name!;

        if (!TryParseSchema(operation.ArgsSchemaJson, out var argsSchema))
        {
            return OpsDescribeServiceResult.Failure(
                $"Operation '{operationName}' args schema is invalid.",
                UcliCoreErrorCodes.InternalError);
        }

        if (!TryParseOptionalSchema(operation.ResultSchemaJson, out var resultSchema))
        {
            return OpsDescribeServiceResult.Failure(
                $"Operation '{operationName}' result schema is invalid.",
                UcliCoreErrorCodes.InternalError);
        }

        if (!TryValidateDescribeContract(operation, out var describeError))
        {
            return OpsDescribeServiceResult.Failure(
                $"Operation '{operationName}' describe contract is invalid: {describeError}",
                UcliCoreErrorCodes.InternalError);
        }

        return OpsDescribeServiceResult.Success(
            new OpsDescribeExecutionOutput(
                Operation: new OpsOperationDetail(
                    name: operation.Name!,
                    kind: operation.Kind!,
                    policy: operation.Policy!,
                    playModeSupport: operation.PlayModeSupport!,
                    description: operation.Description!,
                    inputs: operation.Inputs!,
                    resultContract: operation.ResultContract!,
                    assurance: operation.Assurance!,
                    codeContract: operation.CodeContract,
                    argsSchema: argsSchema,
                    resultSchema: resultSchema),
                ReadIndex: readIndexInfoMapper.Map(output.AccessInfo)),
            $"uCLI ops describe completed for '{operationName}'.");
    }

    private static bool TryParseOptionalSchema (
        string? json,
        out JsonElement? schema)
    {
        if (json == null)
        {
            schema = null;
            return true;
        }

        if (!TryParseSchema(json, out var parsedSchema))
        {
            schema = null;
            return false;
        }

        schema = parsedSchema;
        return true;
    }

    private static bool TryParseSchema (
        string? json,
        out JsonElement schema)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            schema = default;
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                schema = default;
                return false;
            }

            schema = document.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            schema = default;
            return false;
        }
    }

    private static bool TryValidateDescribeContract (
        IndexOpEntryJsonContract operation,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(operation.PlayModeSupport)
            || !ContractLiteralInputParser.IsDefinedIgnoreCase<UcliOperationPlayModeSupport>(operation.PlayModeSupport))
        {
            error = "playModeSupport is missing or unsupported.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(operation.Description))
        {
            error = "description is missing.";
            return false;
        }

        if (operation.Inputs == null)
        {
            error = "inputs is missing.";
            return false;
        }

        if (operation.ResultContract == null
            || string.IsNullOrWhiteSpace(operation.ResultContract.ResultType)
            || string.IsNullOrWhiteSpace(operation.ResultContract.Description))
        {
            error = "resultContract is incomplete.";
            return false;
        }

        if (operation.ResultContract.Emitted && operation.ResultSchemaJson == null)
        {
            error = "resultSchema is required when resultContract.emitted is true.";
            return false;
        }

        if (!operation.ResultContract.Emitted && operation.ResultSchemaJson != null)
        {
            error = "resultSchema must be null when resultContract.emitted is false.";
            return false;
        }

        if (operation.Assurance == null
            || operation.Assurance.SideEffects == null
            || operation.Assurance.TouchedKinds == null
            || operation.Assurance.DangerousNotes == null
            || string.IsNullOrWhiteSpace(operation.Assurance.PlanMode)
            || string.IsNullOrWhiteSpace(operation.Assurance.PlanSemantics)
            || string.IsNullOrWhiteSpace(operation.Assurance.CallSemantics)
            || string.IsNullOrWhiteSpace(operation.Assurance.TouchedContract)
            || string.IsNullOrWhiteSpace(operation.Assurance.ReadPostconditionContract)
            || string.IsNullOrWhiteSpace(operation.Assurance.FailureSemantics))
        {
            error = "assurance is incomplete.";
            return false;
        }

        var describeContract = new UcliOperationDescribeContract(
            operation.Description,
            operation.Inputs,
            operation.ResultContract,
            operation.Assurance,
            operation.CodeContract);
        if (!UcliOperationDescribeContractValidator.TryValidatePublicRawOpDescribeContract(
                describeContract,
                operation.Kind,
                operation.Policy,
                "operation",
                out var describeError))
        {
            error = describeError;
            return false;
        }

        error = null;
        return true;
    }
}
