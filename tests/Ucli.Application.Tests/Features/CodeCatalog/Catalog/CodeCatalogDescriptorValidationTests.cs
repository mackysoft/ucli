using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using CodeCatalogModel = MackySoft.Ucli.Application.Features.CodeCatalog.Catalog.CodeCatalog;

namespace MackySoft.Ucli.Application.Tests.Features.CodeCatalog.Catalog;

public sealed class CodeCatalogDescriptorValidationTests
{
    public static TheoryData<string> InvalidDescriptorCases =>
    [
        "reserved-unknown-kind",
        "invalid-applies-to-command",
        "duplicate-applies-to",
        "empty-appears-in",
        "unknown-related-code",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithDuplicateCode_Throws ()
    {
        var descriptor = CodeCatalogDescriptorTestFactory.CreateErrorDescriptor("DUPLICATE_CODE");
        var duplicateDescriptor = descriptor with
        {
            Summary = "Duplicate descriptor for test.",
        };

        Assert.Throws<InvalidOperationException>(() => new CodeCatalogModel(
            [
                new StaticCodeCatalogContributor([descriptor, duplicateDescriptor]),
            ]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DescriptorConstructor_WithNullCode_ThrowsArgumentNullException ()
    {
        var descriptor = CodeCatalogDescriptorTestFactory.CreateErrorDescriptor("VALID_CODE");

        Assert.Throws<ArgumentNullException>(() => new CodeCatalogDescriptor(
            null!,
            descriptor.Kind,
            descriptor.Category,
            descriptor.Summary,
            descriptor.Meaning,
            descriptor.AppearsIn,
            descriptor.AppliesTo,
            descriptor.CoverageImpact,
            descriptor.VerdictSemantics,
            descriptor.ExecutionSemantics,
            descriptor.Inspect,
            descriptor.RelatedCodes));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void DescriptorConstructor_WithUndefinedKind_ThrowsArgumentOutOfRangeException ()
    {
        var descriptor = CodeCatalogDescriptorTestFactory.CreateErrorDescriptor("VALID_CODE");

        Assert.Throws<ArgumentOutOfRangeException>(() => CreateDescriptorWithKind(
            descriptor,
            (CodeCatalogKind)int.MaxValue));
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(InvalidDescriptorCases))]
    public void Constructor_WithInvalidDescriptor_Throws (string caseName)
    {
        var descriptor = CreateInvalidDescriptor(caseName);

        Assert.Throws<InvalidOperationException>(() => new CodeCatalogModel(
            [
                new StaticCodeCatalogContributor([descriptor]),
            ]));
    }

    private static CodeCatalogDescriptor CreateInvalidDescriptor (string caseName)
    {
        var descriptor = CodeCatalogDescriptorTestFactory.CreateErrorDescriptor("INVALID_DESCRIPTOR_CODE");
        return caseName switch
        {
            "reserved-unknown-kind" => CreateDescriptorWithKind(descriptor, CodeCatalogKind.Unknown),
            "invalid-applies-to-command" => descriptor with
            {
                AppliesTo = [new UcliCommand("unknown.command")],
            },
            "duplicate-applies-to" => descriptor with
            {
                AppliesTo = [UcliCommandIds.Ready, UcliCommandIds.Ready],
            },
            "empty-appears-in" => descriptor with
            {
                AppearsIn = [],
            },
            "unknown-related-code" => descriptor with
            {
                RelatedCodes = [new UcliCode("MISSING_RELATED_CODE")],
            },
            _ => throw new ArgumentOutOfRangeException(nameof(caseName), caseName, "Unknown code catalog descriptor validation case."),
        };
    }

    private static CodeCatalogDescriptor CreateDescriptorWithKind (
        CodeCatalogDescriptor descriptor,
        CodeCatalogKind kind)
    {
        return new CodeCatalogDescriptor(
            descriptor.Code,
            kind,
            descriptor.Category,
            descriptor.Summary,
            descriptor.Meaning,
            descriptor.AppearsIn,
            descriptor.AppliesTo,
            descriptor.CoverageImpact,
            descriptor.VerdictSemantics,
            descriptor.ExecutionSemantics,
            descriptor.Inspect,
            descriptor.RelatedCodes);
    }
}
