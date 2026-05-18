namespace MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

/// <summary> Creates code catalog descriptors from existing code-definition models. </summary>
internal static class CodeCatalogDescriptorFactory
{
    private static readonly IReadOnlyList<string> ErrorAppearsIn = ["errors[].code"];

    /// <summary> Converts one error-code descriptor to the generic code catalog descriptor shape. </summary>
    /// <param name="descriptor"> The existing error-code descriptor. </param>
    /// <returns> A code catalog descriptor with <c>kind=error</c>. </returns>
    public static CodeCatalogDescriptor FromErrorDescriptor (UcliErrorDescriptor descriptor)
    {
        ArgumentNullException.ThrowIfNull(descriptor);

        return new CodeCatalogDescriptor(
            Code: descriptor.Code,
            Kind: CodeCatalogKindValues.Error,
            Category: descriptor.Category,
            Summary: descriptor.Summary,
            Meaning: descriptor.Meaning,
            AppearsIn: ErrorAppearsIn,
            AppliesTo: descriptor.AppliesTo,
            CoverageImpact: null,
            VerdictSemantics: null,
            ExecutionSemantics: descriptor.ExecutionSemantics,
            Inspect: descriptor.Inspect,
            RelatedCodes: descriptor.RelatedCodes);
    }
}
