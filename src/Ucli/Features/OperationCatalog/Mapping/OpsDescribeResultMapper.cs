using System.Text.Json;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Features.OperationCatalog.Access;

namespace MackySoft.Ucli.Features.OperationCatalog.Mapping;

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

        if (!TryParseSchema(operation.ArgsSchemaJson!, out var argsSchema))
        {
            return OpsDescribeServiceResult.Failure(
                $"Operation '{operationName}' args schema is invalid.",
                IpcErrorCodes.InternalError);
        }

        return OpsDescribeServiceResult.Success(
            new OpsDescribeExecutionOutput(
                Operation: new OpsOperationDetail(
                    Name: operation.Name!,
                    Kind: operation.Kind!,
                    Policy: operation.Policy!,
                    ArgsSchema: argsSchema),
                ReadIndex: readIndexInfoMapper.Map(output.AccessInfo)),
            $"uCLI ops describe completed for '{operationName}'.");
    }

    private static bool TryParseSchema (
        string json,
        out JsonElement schema)
    {
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
}