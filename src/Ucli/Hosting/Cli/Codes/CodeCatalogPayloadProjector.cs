using System.Text.Json.Serialization;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Hosting.Cli.Codes;

/// <summary> Projects code catalog application models into public CLI payload shapes. </summary>
internal static class CodeCatalogPayloadProjector
{
    private const int CatalogVersion = 1;

    private const string Source = "bundled";

    private static readonly IReadOnlyList<string> ListedKindLiterals = Enum
        .GetValues<CodeCatalogKind>()
        .Where(static kind => kind != CodeCatalogKind.Unknown)
        .Select(ContractLiteralCodec.ToValue)
        .ToArray();

    /// <summary> Creates the public payload for <c>codes list</c>. </summary>
    /// <param name="result"> The successful application list result. </param>
    /// <returns> The JSON-serializable payload. </returns>
    public static object CreateListPayload (CodeCatalogListResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ListPayload(
            CatalogVersion,
            Source,
            ListedKindLiterals,
            result.Descriptors!.Select(static descriptor => new CodeListItemPayload(
                descriptor.Code.Value,
                ContractLiteralCodec.ToValue(descriptor.Kind),
                descriptor.Category,
                descriptor.Summary)).ToArray());
    }

    /// <summary> Creates the public payload for <c>codes describe</c>. </summary>
    /// <param name="result"> The successful application describe result. </param>
    /// <returns> The JSON-serializable payload. </returns>
    public static object CreateDescribePayload (CodeCatalogDescribeResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var descriptor = result.Descriptor!;
        return new DescribePayload(
            descriptor.Code.Value,
            result.Known,
            ContractLiteralCodec.ToValue(descriptor.Kind),
            descriptor.Category,
            descriptor.Summary,
            descriptor.Meaning,
            descriptor.AppearsIn,
            NullIfEmpty(descriptor.AppliesTo.Select(static command => command.Name).ToArray()),
            descriptor.CoverageImpact,
            descriptor.VerdictSemantics,
            descriptor.ExecutionSemantics,
            NullIfEmpty(descriptor.Inspect),
            NullIfEmpty(descriptor.RelatedCodes.Select(static code => code.Value).ToArray()));
    }

    private static IReadOnlyList<string>? NullIfEmpty (IReadOnlyList<string> values)
    {
        return values.Count == 0 ? null : values;
    }

    private sealed record ListPayload (
        int CatalogVersion,
        string Source,
        IReadOnlyList<string> Kinds,
        IReadOnlyList<CodeListItemPayload> Codes);

    private sealed record CodeListItemPayload (
        string Code,
        string Kind,
        string Category,
        string Summary);

    private sealed record DescribePayload (
        string Code,
        bool Known,
        string Kind,
        string Category,
        string Summary,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        string? Meaning,
        IReadOnlyList<string> AppearsIn,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<string>? AppliesTo,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        object? CoverageImpact,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        object? VerdictSemantics,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        UcliErrorExecutionSemantics? ExecutionSemantics,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<string>? Inspect,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        IReadOnlyList<string>? RelatedCodes);
}
