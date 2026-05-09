using System.Reflection;
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
        var expectedCodes = GetErrorCodeDefinitions(typeof(ApplicationErrorCodeDescriptors).Assembly);

        foreach (var expectedCode in expectedCodes)
        {
            Assert.Contains(expectedCode, actualCodes);
        }
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

    private static IReadOnlySet<UcliErrorCode> GetErrorCodeDefinitions (Assembly assembly)
    {
        var codes = new HashSet<UcliErrorCode>();
        var types = assembly.GetTypes()
            .Where(static type => type is { IsClass: true, IsAbstract: true, IsSealed: true }
                && type.Name.EndsWith("ErrorCodes", StringComparison.Ordinal));

        foreach (var type in types)
        {
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(UcliErrorCode))
                {
                    codes.Add((UcliErrorCode)field.GetValue(null)!);
                }
            }
        }

        return codes;
    }
}
