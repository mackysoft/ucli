using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Source;
using MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;
using MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Filtering;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Projection;

/// <summary> Implements mapping from catalog snapshots to command-facing <c>ops list</c> results. </summary>
internal sealed class OpsListResultMapper : IOpsListResultMapper
{
    private readonly OpsReadIndexInfoMapper readIndexInfoMapper;

    /// <summary> Initializes a new instance of the <see cref="OpsListResultMapper" /> class. </summary>
    /// <param name="readIndexInfoMapper"> The read-index info mapper dependency. </param>
    public OpsListResultMapper (OpsReadIndexInfoMapper readIndexInfoMapper)
    {
        this.readIndexInfoMapper = readIndexInfoMapper ?? throw new ArgumentNullException(nameof(readIndexInfoMapper));
    }

    /// <inheritdoc />
    public OpsListServiceResult Map (
        OpsListReadOutput output,
        OpsListFilter filter)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(filter);

        var matchedOperations = new List<OpsCatalogListEntry>();
        foreach (var operation in output.Snapshot.Operations)
        {
            if (!TryIsMatch(operation, filter, out var isMatch, out var errorMessage))
            {
                return OpsListServiceResult.Failure(errorMessage!, UcliCoreErrorCodes.InvalidArgument);
            }

            if (isMatch)
            {
                matchedOperations.Add(operation);
            }
        }

        var operations = matchedOperations
            .OrderBy(static operation => operation.Name, StringComparer.Ordinal)
            .Select(static operation => new OpsOperationListItem(
                Name: operation.Name!,
                Kind: operation.Kind!,
                Policy: operation.Policy!))
            .ToArray();

        return OpsListServiceResult.Success(
            new OpsListExecutionOutput(
                Operations: operations,
                ReadIndex: readIndexInfoMapper.Map(output.AccessInfo)),
            "uCLI ops list completed.");
    }

    private static bool TryIsMatch (
        OpsCatalogListEntry operation,
        OpsListFilter filter,
        out bool isMatch,
        out string? errorMessage)
    {
        if (filter.NameRegex != null)
        {
            if (!RegexPatternUtilities.TryIsMatch(operation.Name, filter.NameRegex, out var regexMatch))
            {
                isMatch = false;
                errorMessage = "nameRegex match timed out.";
                return false;
            }

            if (!regexMatch)
            {
                isMatch = false;
                errorMessage = null;
                return true;
            }
        }

        if (filter.Kind.HasValue)
        {
            if (!UcliOperationKindCodec.TryParse(operation.Kind, out var operationKind)
                || operationKind != filter.Kind.Value)
            {
                isMatch = false;
                errorMessage = null;
                return true;
            }
        }

        if (filter.MaxPolicy.HasValue)
        {
            if (!OperationPolicyCodec.TryParse(operation.Policy, out var operationPolicy)
                || operationPolicy > filter.MaxPolicy.Value)
            {
                isMatch = false;
                errorMessage = null;
                return true;
            }
        }

        isMatch = true;
        errorMessage = null;
        return true;
    }
}
