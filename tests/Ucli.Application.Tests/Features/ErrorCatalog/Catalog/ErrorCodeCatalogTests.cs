using MackySoft.Tests;
using MackySoft.Ucli.Application.Diagnostics;
using MackySoft.Ucli.Application.Features.ErrorCatalog.Catalog;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests.Features.ErrorCatalog.Catalog;

public sealed class ErrorCodeCatalogTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithApplicationContributors_OrdersDescriptorsByCode ()
    {
        var catalog = CreateCatalog();
        var actualCodes = catalog.Descriptors
            .Select(static descriptor => descriptor.Code.Value)
            .ToArray();
        var expectedCodes = actualCodes
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedCodes, actualCodes);
        Assert.Contains(IpcTransportErrorCodes.IpcTimeout, catalog.Descriptors.Select(static descriptor => descriptor.Code));
        Assert.Contains(ProjectContextErrorCodes.ProjectPathNotFound, catalog.Descriptors.Select(static descriptor => descriptor.Code));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithApplicationContributors_ContainsEveryApplicationErrorCodeDefinition ()
    {
        var catalog = CreateCatalog();
        var actualCodes = catalog.Descriptors
            .Select(static descriptor => descriptor.Code)
            .ToHashSet();
        var expectedCodes = StaticFieldValueReader.ReadFromStaticClasses<UcliErrorCode>(
            typeof(ApplicationErrorCodeDescriptors).Assembly,
            "ErrorCodes");

        foreach (var expectedCode in expectedCodes)
        {
            Assert.Contains(expectedCode, actualCodes);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithApplicationContributors_DescriptorsDoNotUseBroadInspectTargets ()
    {
        var catalog = CreateCatalog();

        foreach (var descriptor in catalog.Descriptors)
        {
            ErrorInspectTargetAssert.DoesNotUseBroadOrSensitiveTargets(descriptor.Inspect);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithoutFilters_ReturnsDescriptorsOrderedByCode ()
    {
        var service = new ErrorCodeCatalogService(CreateCatalog());

        var result = service.List(new ErrorCodeCatalogListInput(Category: null, Command: null));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        var actualCodes = result.Descriptors!
            .Select(static descriptor => descriptor.Code.Value)
            .ToArray();
        var expectedCodes = actualCodes
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedCodes, actualCodes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithCategoryFilter_ReturnsExactCategoryMatches ()
    {
        var service = new ErrorCodeCatalogService(CreateCatalog());

        var result = service.List(new ErrorCodeCatalogListInput(Category: "transport", Command: null));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        Assert.NotEmpty(result.Descriptors);
        Assert.All(result.Descriptors!, static descriptor => Assert.Equal("transport", descriptor.Category));
        Assert.Contains(IpcTransportErrorCodes.IpcTimeout, result.Descriptors!.Select(static descriptor => descriptor.Code));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithCommandLeafFilter_ReturnsFamilyMatches ()
    {
        var service = new ErrorCodeCatalogService(CreateCatalog());

        var result = service.List(new ErrorCodeCatalogListInput(Category: null, Command: "query.assets.find"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        Assert.Contains(IpcTransportErrorCodes.IpcTimeout, result.Descriptors!.Select(static descriptor => descriptor.Code));
        Assert.Contains(UcliCoreErrorCodes.InvalidArgument, result.Descriptors.Select(static descriptor => descriptor.Code));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithEveryDescriptorCommandFilter_ReturnsOwningDescriptor ()
    {
        var catalog = CreateCatalog();
        var service = new ErrorCodeCatalogService(catalog);

        foreach (var descriptor in catalog.Descriptors)
        {
            foreach (var command in descriptor.AppliesTo)
            {
                var result = service.List(new ErrorCodeCatalogListInput(Category: null, Command: command.Name));

                Assert.True(result.IsSuccess);
                Assert.NotNull(result.Descriptors);
                Assert.Contains(result.Descriptors!, candidate => candidate.Code == descriptor.Code);
            }
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithUnknownCommandFilter_ReturnsEmptyDescriptors ()
    {
        var service = new ErrorCodeCatalogService(CreateCatalog());

        var result = service.List(new ErrorCodeCatalogListInput(Category: null, Command: "query.unknown"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        Assert.Empty(result.Descriptors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithInvalidCommandFilter_ReturnsInvalidArgument ()
    {
        var service = new ErrorCodeCatalogService(CreateCatalog());

        var result = service.List(new ErrorCodeCatalogListInput(Category: null, Command: "query assets"));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Descriptors);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithEmptyCategoryFilter_ReturnsInvalidArgument ()
    {
        var service = new ErrorCodeCatalogService(CreateCatalog());

        var result = service.List(new ErrorCodeCatalogListInput(Category: " ", Command: null));

        Assert.False(result.IsSuccess);
        Assert.Null(result.Descriptors);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithUnknownCategory_ReturnsEmptyDescriptors ()
    {
        var service = new ErrorCodeCatalogService(CreateCatalog());

        var result = service.List(new ErrorCodeCatalogListInput(Category: "unknown-category", Command: null));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        Assert.Empty(result.Descriptors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Describe_WithUnknownCodeAndRequireKnownFalse_ReturnsFallbackDescriptor ()
    {
        var service = new ErrorCodeCatalogService(CreateCatalog());
        var futureCode = new UcliErrorCode("SOME_FUTURE_CODE");

        var result = service.Describe(futureCode, requireKnown: false);

        Assert.True(result.IsSuccess);
        Assert.False(result.Known);
        Assert.NotNull(result.Descriptor);
        Assert.Equal(futureCode, result.Descriptor!.Code);
        Assert.Equal("unknown", result.Descriptor.Category);
        Assert.Null(result.Descriptor.ExecutionSemantics.ImpliesNotApplied);
        Assert.True(result.Descriptor.ExecutionSemantics.MayBeIndeterminate);
        Assert.Equal(UcliErrorRetryClassValues.Unknown, result.Descriptor.ExecutionSemantics.SafeToRetry);
        ErrorInspectTargetAssert.DoesNotUseBroadOrSensitiveTargets(result.Descriptor.Inspect);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Describe_WithUnknownCodeAndRequireKnownTrue_ReturnsInvalidArgument ()
    {
        var service = new ErrorCodeCatalogService(CreateCatalog());

        var result = service.Describe(new UcliErrorCode("SOME_FUTURE_CODE"), requireKnown: true);

        Assert.False(result.IsSuccess);
        Assert.False(result.Known);
        Assert.Null(result.Descriptor);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Describe_WithInvalidCode_ReturnsInvalidArgument ()
    {
        var service = new ErrorCodeCatalogService(CreateCatalog());

        var result = service.Describe(default, requireKnown: false);

        Assert.False(result.IsSuccess);
        Assert.False(result.Known);
        Assert.Null(result.Descriptor);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithDuplicateCode_Throws ()
    {
        var descriptor = UcliKnownErrorCodeDescriptors.All[0];
        var duplicateDescriptor = descriptor with
        {
            Summary = "Duplicate descriptor for test.",
        };

        Assert.Throws<InvalidOperationException>(() => new ErrorCodeCatalog(
            [
                new StubContributor([descriptor, duplicateDescriptor]),
            ]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithInvalidAppliesToCommand_Throws ()
    {
        var descriptor = UcliKnownErrorCodeDescriptors.All[0] with
        {
            AppliesTo = [default],
        };

        Assert.Throws<InvalidOperationException>(() => new ErrorCodeCatalog(
            [
                new StubContributor([descriptor]),
            ]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithEmptyPossiblePhase_Throws ()
    {
        var descriptor = UcliKnownErrorCodeDescriptors.All[0] with
        {
            PossiblePhases = [" "],
        };

        Assert.Throws<InvalidOperationException>(() => new ErrorCodeCatalog(
            [
                new StubContributor([descriptor]),
            ]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithEmptyInspectItem_Throws ()
    {
        var descriptor = UcliKnownErrorCodeDescriptors.All[0] with
        {
            Inspect = [string.Empty],
        };

        Assert.Throws<InvalidOperationException>(() => new ErrorCodeCatalog(
            [
                new StubContributor([descriptor]),
            ]));
    }

    private static ErrorCodeCatalog CreateCatalog ()
    {
        return new ErrorCodeCatalog(
            [
                new ContractsErrorCodeCatalogContributor(),
                new ApplicationErrorCodeCatalogContributor(),
            ]);
    }

    private sealed class StubContributor : IErrorCodeCatalogContributor
    {
        private readonly IReadOnlyList<UcliErrorCodeDescriptor> descriptors;

        public StubContributor (IReadOnlyList<UcliErrorCodeDescriptor> descriptors)
        {
            this.descriptors = descriptors;
        }

        public IReadOnlyList<UcliErrorCodeDescriptor> GetDescriptors ()
        {
            return descriptors;
        }
    }

}
