using System.Text.RegularExpressions;
using MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.OperationCatalog.UseCases.Ops.Filtering;

/// <summary> Represents compiled <c>ops list</c> filters. </summary>
internal sealed record OpsListFilter (
    Regex? NameRegex,
    UcliOperationKind? Kind,
    OperationPolicy? MaxPolicy)
{
    /// <summary> Creates a compiled filter from normalized command input. </summary>
    public static bool TryCreate (
        OpsCommandInput input,
        out OpsListFilter? filter,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(input);

        Regex? regex = null;
        if (input.NameRegex != null)
        {
            if (string.IsNullOrWhiteSpace(input.NameRegex))
            {
                filter = null;
                errorMessage = "nameRegex must not be empty.";
                return false;
            }

            if (!RegexPatternUtilities.TryCompilePattern(input.NameRegex, out regex, out var regexError))
            {
                filter = null;
                errorMessage = $"nameRegex is invalid. {regexError}";
                return false;
            }
        }

        filter = new OpsListFilter(regex, input.Kind, input.MaxPolicy);
        errorMessage = null;
        return true;
    }

    /// <summary> Applies this filter to one operation list. </summary>
    public OpsListFilterApplyResult Apply (IReadOnlyList<OpsCatalogListEntry> operations)
    {
        ArgumentNullException.ThrowIfNull(operations);

        if (NameRegex == null && !Kind.HasValue && !MaxPolicy.HasValue)
        {
            return OpsListFilterApplyResult.Success(operations);
        }

        var matchedOperations = new List<OpsCatalogListEntry>();
        foreach (var operation in operations)
        {
            if (!TryIsMatch(operation, out var isMatch, out var errorMessage))
            {
                return OpsListFilterApplyResult.Failure(errorMessage!);
            }

            if (isMatch)
            {
                matchedOperations.Add(operation);
            }
        }

        return OpsListFilterApplyResult.Success(matchedOperations);
    }

    private bool TryIsMatch (
        OpsCatalogListEntry operation,
        out bool isMatch,
        out string? errorMessage)
    {
        if (NameRegex != null)
        {
            if (!RegexPatternUtilities.TryIsMatch(operation.Name, NameRegex, out var regexMatch))
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

        if (Kind.HasValue && operation.Kind != Kind.Value)
        {
            isMatch = false;
            errorMessage = null;
            return true;
        }

        if (MaxPolicy.HasValue && operation.Policy > MaxPolicy.Value)
        {
            isMatch = false;
            errorMessage = null;
            return true;
        }

        isMatch = true;
        errorMessage = null;
        return true;
    }
}
