using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using CodeCatalogModel = MackySoft.Ucli.Application.Features.CodeCatalog.Catalog.CodeCatalog;

namespace MackySoft.Ucli.Application.Tests.Features.CodeCatalog.Catalog;

public sealed class CodeCatalogDescriptorValidationTests
{
    public static TheoryData<string> InvalidDescriptorCases =>
    [
        "default-code",
        "unsupported-kind",
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
            "default-code" => descriptor with
            {
                Code = default,
            },
            "unsupported-kind" => descriptor with
            {
                Kind = "unknown-kind",
            },
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
}
