using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Tests.Features.CodeCatalog.Catalog;

public sealed class CodeCatalogServiceDescribeTests
{
    private static readonly CodeCatalogKind[] KnownCodeKindMismatchCases =
    [
        CodeCatalogKind.Claim,
        CodeCatalogKind.Unknown,
    ];

    private static readonly CodeCatalogKind[] UnknownCodeFallbackCases =
    [
        CodeCatalogKind.Error,
        CodeCatalogKind.Unknown,
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void Describe_WithKnownCodeAndExpectedKind_ReturnsDescriptor ()
    {
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.Describe(
            new CodeCatalogCodeReference(IpcTransportErrorCodes.IpcTimeout, CodeCatalogKind.Error),
            requireKnown: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Known);
        Assert.NotNull(result.Descriptor);
        Assert.Equal(IpcTransportErrorCodes.IpcTimeout, result.Descriptor!.Code);
        Assert.Equal(CodeCatalogKind.Error, result.Descriptor.Kind);
        Assert.Contains("errors[].code", result.Descriptor.AppearsIn);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Describe_WithKnownCodeAndMismatchedExpectedKind_ReturnsInvalidArgument ()
    {
        var service = CodeCatalogTestSupport.CreateService();

        foreach (var expectedKind in KnownCodeKindMismatchCases)
        {
            var result = service.Describe(
                new CodeCatalogCodeReference(IpcTransportErrorCodes.IpcTimeout, expectedKind),
                requireKnown: false);

            CodeCatalogTestSupport.AssertInvalidArgument(result);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Describe_WithUnknownCodeAndRequireKnownFalse_ReturnsUnknownFallback ()
    {
        var service = CodeCatalogTestSupport.CreateService();
        var futureCode = new UcliCode("SOME_FUTURE_CODE");

        foreach (var expectedKind in UnknownCodeFallbackCases)
        {
            var result = service.Describe(
                new CodeCatalogCodeReference(futureCode, expectedKind),
                requireKnown: false);

            Assert.True(result.IsSuccess);
            Assert.False(result.Known);
            Assert.NotNull(result.Descriptor);
            Assert.Equal(futureCode, result.Descriptor!.Code);
            Assert.Equal(CodeCatalogKind.Unknown, result.Descriptor.Kind);
            Assert.Equal(TextVocabulary.GetText(CodeCatalogKind.Unknown), result.Descriptor.Category);
            Assert.Empty(result.Descriptor.AppearsIn);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Describe_WithUnknownCodeAndRequireKnownTrue_ReturnsInvalidArgument ()
    {
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.Describe(
            new CodeCatalogCodeReference(new UcliCode("SOME_FUTURE_CODE"), ExpectedKind: null),
            requireKnown: true);

        CodeCatalogTestSupport.AssertInvalidArgument(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CodeCatalogCodeReference_WithNullCode_ThrowsArgumentNullException ()
    {
        Assert.Throws<ArgumentNullException>(() => new CodeCatalogCodeReference(
            null!,
            ExpectedKind: null));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void CodeCatalogCodeReference_WithUndefinedExpectedKind_ThrowsArgumentOutOfRangeException ()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CodeCatalogCodeReference(
            IpcTransportErrorCodes.IpcTimeout,
            (CodeCatalogKind)int.MaxValue));
    }
}
