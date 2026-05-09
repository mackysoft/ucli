using System.Text.Json.Serialization;
using MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;

namespace MackySoft.Ucli.Hosting.Cli.Errors;

/// <summary> Projects error-code catalog application models into public CLI payload shapes. </summary>
internal static class ErrorCodeCatalogPayloadProjector
{
    private const int CatalogVersion = 1;

    private const string Source = "bundled";

    /// <summary> Creates the public payload for <c>errors list</c>. </summary>
    /// <param name="result"> The successful application list result. </param>
    /// <returns> The JSON-serializable payload. </returns>
    public static object CreateListPayload (ErrorCodeCatalogListResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new
        {
            catalogVersion = CatalogVersion,
            source = Source,
            codes = result.Descriptors!.Select(static descriptor => new
            {
                code = descriptor.Code.Value,
                category = descriptor.Category,
                summary = descriptor.Summary,
            }).ToArray(),
        };
    }

    /// <summary> Creates the public payload for <c>errors describe</c>. </summary>
    /// <param name="result"> The successful application describe result. </param>
    /// <returns> The JSON-serializable payload. </returns>
    public static object CreateDescribePayload (ErrorCodeCatalogDescribeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var descriptor = result.Descriptor!;
        return new
        {
            code = descriptor.Code.Value,
            known = result.Known,
            category = descriptor.Category,
            summary = descriptor.Summary,
            meaning = descriptor.Meaning,
            appliesTo = descriptor.AppliesTo.Select(static command => command.Name).ToArray(),
            possiblePhases = descriptor.PossiblePhases,
            executionSemantics = descriptor.ExecutionSemantics,
            inspect = descriptor.Inspect,
            nextActions = descriptor.NextActions.Select(static action => new NextActionPayload(action.When, action.Action)).ToArray(),
            relatedCodes = descriptor.RelatedCodes.Select(static code => code.Value).ToArray(),
        };
    }

    private sealed record NextActionPayload (
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? When,
        string Action);
}
