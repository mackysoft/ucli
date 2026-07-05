using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;

namespace MackySoft.Ucli.Application.Tests.Features.CodeCatalog.Catalog;

public sealed class CodeCatalogServiceDescribeTests
{
    public static TheoryData<string> KnownCodeKindMismatchCases =>
    [
        CodeCatalogKindValues.Claim,
        "future-kind",
    ];

    public static TheoryData<string> UnknownCodeFallbackCases =>
    [
        CodeCatalogKindValues.Error,
        "future-kind",
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void Describe_WithKnownCodeAndExpectedKind_ReturnsDescriptor ()
    {
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.Describe(
            new CodeCatalogCodeReference(IpcTransportErrorCodes.IpcTimeout, CodeCatalogKindValues.Error),
            requireKnown: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Known);
        Assert.NotNull(result.Descriptor);
        Assert.Equal(IpcTransportErrorCodes.IpcTimeout, result.Descriptor!.Code);
        Assert.Equal(CodeCatalogKindValues.Error, result.Descriptor.Kind);
        Assert.Contains("errors[].code", result.Descriptor.AppearsIn);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(KnownCodeKindMismatchCases))]
    public void Describe_WithKnownCodeAndMismatchedExpectedKind_ReturnsInvalidArgument (string expectedKind)
    {
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.Describe(
            new CodeCatalogCodeReference(IpcTransportErrorCodes.IpcTimeout, expectedKind),
            requireKnown: false);

        CodeCatalogTestSupport.AssertInvalidArgument(result);
    }

    [Theory]
    [Trait("Size", "Small")]
    [MemberData(nameof(UnknownCodeFallbackCases))]
    public void Describe_WithUnknownCodeAndRequireKnownFalse_ReturnsUnknownFallback (string expectedKind)
    {
        var service = CodeCatalogTestSupport.CreateService();
        var futureCode = new UcliCode("SOME_FUTURE_CODE");

        var result = service.Describe(
            new CodeCatalogCodeReference(futureCode, expectedKind),
            requireKnown: false);

        Assert.True(result.IsSuccess);
        Assert.False(result.Known);
        Assert.NotNull(result.Descriptor);
        Assert.Equal(futureCode, result.Descriptor!.Code);
        Assert.Equal(CodeCatalogKindValues.Unknown, result.Descriptor.Kind);
        Assert.Equal(CodeCatalogKindValues.Unknown, result.Descriptor.Category);
        Assert.Empty(result.Descriptor.AppearsIn);
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
    public void Describe_WithDefaultCodeValue_ReturnsInvalidArgument ()
    {
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.Describe(new CodeCatalogCodeReference(default, ExpectedKind: null), requireKnown: false);

        CodeCatalogTestSupport.AssertInvalidArgument(result);
    }
}
