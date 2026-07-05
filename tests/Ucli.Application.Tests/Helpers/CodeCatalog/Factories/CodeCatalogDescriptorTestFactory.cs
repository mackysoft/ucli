using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Tests;

internal static class CodeCatalogDescriptorTestFactory
{
    internal static CodeCatalogDescriptor CreateErrorDescriptor (string code)
    {
        return new CodeCatalogDescriptor(
            Code: new UcliCode(code),
            Kind: CodeCatalogKindValues.Error,
            Category: "test",
            Summary: "Test descriptor.",
            Meaning: "A test descriptor.",
            AppearsIn: ["errors[].code"],
            AppliesTo: [UcliCommandIds.Status],
            CoverageImpact: null,
            VerdictSemantics: null,
            ExecutionSemantics: new UcliErrorExecutionSemantics(
                ImpliesNotApplied: true,
                MayBeIndeterminate: false,
                SafeToRetry: UcliErrorRetryClassValues.No),
            Inspect: ["errors[].code"],
            RelatedCodes: []);
    }
}
