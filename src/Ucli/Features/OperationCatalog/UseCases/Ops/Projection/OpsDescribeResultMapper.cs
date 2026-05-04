using System.Text.Json;
using MackySoft.Ucli.Contracts.Index;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Features.OperationCatalog.Common.Contracts;

namespace MackySoft.Ucli.Features.OperationCatalog.UseCases.Ops.Projection;

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
    public OpsDescribeServiceResult Map (
        OpsCatalogReadOutput output,
        string? operationName)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (string.IsNullOrWhiteSpace(operationName))
        {
            return OpsDescribeServiceResult.Failure(
                "Operation name must not be empty.",
                IpcErrorCodes.InvalidArgument);
        }

        var operation = output.Operations
            .FirstOrDefault(operation => string.Equals(operation.Name, operationName, StringComparison.Ordinal));
        if (operation == null)
        {
            return OpsDescribeServiceResult.Failure(
                $"Operation '{operationName}' is not available.",
                IpcErrorCodes.InvalidArgument);
        }

        if (!TryParseSchema(operation.ArgsSchemaJson, out var argsSchema))
        {
            return OpsDescribeServiceResult.Failure(
                $"Operation '{operationName}' args schema is invalid.",
                IpcErrorCodes.InternalError);
        }

        if (!TryParseOptionalSchema(operation.ResultSchemaJson, out var resultSchema))
        {
            return OpsDescribeServiceResult.Failure(
                $"Operation '{operationName}' result schema is invalid.",
                IpcErrorCodes.InternalError);
        }

        if (!TryValidateDescribeContract(operation, out var describeError))
        {
            return OpsDescribeServiceResult.Failure(
                $"Operation '{operationName}' describe contract is invalid: {describeError}",
                IpcErrorCodes.InternalError);
        }

        return OpsDescribeServiceResult.Success(
            new OpsDescribeExecutionOutput(
                Operation: new OpsOperationDetail(
                    name: operation.Name!,
                    kind: operation.Kind!,
                    policy: operation.Policy!,
                    description: operation.Description!,
                    inputs: operation.Inputs!,
                    resultContract: operation.ResultContract!,
                    assurance: operation.Assurance!,
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
            || string.IsNullOrWhiteSpace(operation.Assurance.PlanMode))
        {
            error = "assurance is incomplete.";
            return false;
        }

        error = null;
        return true;
    }
}
