using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;

namespace MackySoft.Ucli.Hosting.Cli.Codes;

/// <summary> Projects code catalog application models into public CLI payload shapes. </summary>
internal static class CodeCatalogPayloadProjector
{
    private const int CatalogVersion = 1;

    private static readonly IReadOnlyList<CodeCatalogKind> ListedKinds = Enum
        .GetValues<CodeCatalogKind>()
        .Where(static kind => kind != CodeCatalogKind.Unknown)
        .ToArray();

    /// <summary> Gets the serializer contract used by <c>codes list</c> payloads. </summary>
    public static JsonTypeInfo ListPayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(ListPayload));

    /// <summary> Gets the serializer contract used by <c>codes describe</c> payloads. </summary>
    public static JsonTypeInfo DescribePayloadTypeInfo { get; } =
        CliOutputJsonSerializerOptions.Default.GetTypeInfo(typeof(DescribePayload));

    /// <summary> Creates the public payload for <c>codes list</c>. </summary>
    /// <param name="result"> The successful application list result. </param>
    /// <returns> The JSON-serializable payload. </returns>
    public static object CreateListPayload (CodeCatalogListResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ListPayload(
            CatalogVersion,
            CodeCatalogSource.Bundled,
            ListedKinds,
            result.Descriptors!.Select(static descriptor => new CodeListItemPayload(
                descriptor.Code.Value,
                descriptor.Kind,
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
            descriptor.Kind,
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
        CodeCatalogSource Source,
        IReadOnlyList<CodeCatalogKind> Kinds,
        IReadOnlyList<CodeListItemPayload> Codes);

    private sealed record CodeListItemPayload (
        string Code,
        CodeCatalogKind Kind,
        string Category,
        string Summary);

    private sealed record DescribePayload (
        string Code,
        bool Known,
        CodeCatalogKind Kind,
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
