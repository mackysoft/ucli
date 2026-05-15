using MackySoft.Tests;
using MackySoft.Ucli.Application.Diagnostics;
using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Application.Shared.Foundation;
using CodeCatalogModel = MackySoft.Ucli.Application.Features.CodeCatalog.Catalog.CodeCatalog;

namespace MackySoft.Ucli.Application.Tests.Features.CodeCatalog.Catalog;

public sealed class CodeCatalogTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithApplicationContributors_OrdersDescriptorsByCode ()
    {
        var catalog = CreateCatalog();
        var actualCodes = catalog.Descriptors
            .Select(static descriptor => descriptor.Code)
            .ToArray();
        var expectedCodes = actualCodes
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedCodes, actualCodes);
        Assert.Contains(IpcTransportErrorCodes.IpcTimeout.Value, actualCodes);
        Assert.Contains(ProjectContextErrorCodes.ProjectPathNotFound.Value, actualCodes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithApplicationContributors_ContainsEveryApplicationErrorCodeDefinition ()
    {
        var catalog = CreateCatalog();
        var actualCodes = catalog.Descriptors
            .Select(static descriptor => new UcliErrorCode(descriptor.Code))
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
    public void Constructor_WithApplicationContributors_ConvertsErrorsToErrorKind ()
    {
        var catalog = CreateCatalog();

        foreach (var descriptor in catalog.Descriptors)
        {
            Assert.Equal(CodeCatalogKindValues.Error, descriptor.Kind);
            Assert.Contains("errors[].code", descriptor.AppearsIn);
            ErrorInspectTargetAssert.DoesNotUseBroadOrSensitiveTargets(descriptor.Inspect);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void KnownKinds_ExposeSupportedKindsWithoutUnknown ()
    {
        Assert.Equal(
            [
                CodeCatalogKindValues.Error,
                CodeCatalogKindValues.Diagnostic,
                CodeCatalogKindValues.Reason,
                CodeCatalogKindValues.Claim,
                CodeCatalogKindValues.Risk,
            ],
            CodeCatalogKindValues.KnownKinds);
        Assert.DoesNotContain(CodeCatalogKindValues.Unknown, CodeCatalogKindValues.KnownKinds);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithoutFilters_ReturnsDescriptorsOrderedByCode ()
    {
        var service = new CodeCatalogService(CreateCatalog());

        var result = service.List(new CodeCatalogListInput(Kind: null, Command: null));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        var actualCodes = result.Descriptors!
            .Select(static descriptor => descriptor.Code)
            .ToArray();
        var expectedCodes = actualCodes
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(expectedCodes, actualCodes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithKindFilter_ReturnsExactKindMatches ()
    {
        var service = new CodeCatalogService(CreateCatalog());

        var result = service.List(new CodeCatalogListInput(Kind: CodeCatalogKindValues.Error, Command: null));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        Assert.NotEmpty(result.Descriptors);
        Assert.All(result.Descriptors!, static descriptor => Assert.Equal(CodeCatalogKindValues.Error, descriptor.Kind));
        Assert.Contains(IpcTransportErrorCodes.IpcTimeout.Value, result.Descriptors!.Select(static descriptor => descriptor.Code));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithUnknownKindFilter_ReturnsEmptyDescriptors ()
    {
        var service = new CodeCatalogService(CreateCatalog());

        var result = service.List(new CodeCatalogListInput(Kind: "unknown-kind", Command: null));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        Assert.Empty(result.Descriptors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithCommandLeafFilter_ReturnsFamilyMatches ()
    {
        var service = new CodeCatalogService(CreateCatalog());

        var result = service.List(new CodeCatalogListInput(Kind: null, Command: "query.assets.find"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        Assert.Contains(IpcTransportErrorCodes.IpcTimeout.Value, result.Descriptors!.Select(static descriptor => descriptor.Code));
        Assert.Contains(UcliCoreErrorCodes.InvalidArgument.Value, result.Descriptors.Select(static descriptor => descriptor.Code));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithEveryDescriptorCommandFilter_ReturnsOwningDescriptor ()
    {
        var catalog = CreateCatalog();
        var service = new CodeCatalogService(catalog);

        foreach (var descriptor in catalog.Descriptors)
        {
            foreach (var command in descriptor.AppliesTo)
            {
                var result = service.List(new CodeCatalogListInput(Kind: null, Command: command.Name));

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
        var service = new CodeCatalogService(CreateCatalog());

        var result = service.List(new CodeCatalogListInput(Kind: null, Command: "query.unknown"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        Assert.Empty(result.Descriptors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithInvalidCommandFilter_ReturnsInvalidArgument ()
    {
        var service = new CodeCatalogService(CreateCatalog());

        var result = service.List(new CodeCatalogListInput(Kind: null, Command: "query assets"));

        AssertInvalidArgument(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithEmptyKindFilter_ReturnsInvalidArgument ()
    {
        var service = new CodeCatalogService(CreateCatalog());

        var result = service.List(new CodeCatalogListInput(Kind: " ", Command: null));

        AssertInvalidArgument(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Describe_WithKnownCodeAndExpectedKind_ReturnsDescriptor ()
    {
        var service = new CodeCatalogService(CreateCatalog());

        var result = service.Describe(
            new CodeCatalogCodeReference(IpcTransportErrorCodes.IpcTimeout.Value, CodeCatalogKindValues.Error),
            requireKnown: true);

        Assert.True(result.IsSuccess);
        Assert.True(result.Known);
        Assert.NotNull(result.Descriptor);
        Assert.Equal(IpcTransportErrorCodes.IpcTimeout.Value, result.Descriptor!.Code);
        Assert.Equal(CodeCatalogKindValues.Error, result.Descriptor.Kind);
        Assert.Contains("errors[].code", result.Descriptor.AppearsIn);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Describe_WithKnownCodeAndWrongExpectedKind_ReturnsInvalidArgument ()
    {
        var service = new CodeCatalogService(CreateCatalog());

        var result = service.Describe(
            new CodeCatalogCodeReference(IpcTransportErrorCodes.IpcTimeout.Value, CodeCatalogKindValues.Claim),
            requireKnown: true);

        Assert.False(result.IsSuccess);
        Assert.False(result.Known);
        Assert.Null(result.Descriptor);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Describe_WithKnownCodeAndFutureExpectedKind_ReturnsInvalidArgument ()
    {
        var service = new CodeCatalogService(CreateCatalog());

        var result = service.Describe(
            new CodeCatalogCodeReference(IpcTransportErrorCodes.IpcTimeout.Value, "future-kind"),
            requireKnown: false);

        Assert.False(result.IsSuccess);
        Assert.False(result.Known);
        Assert.Null(result.Descriptor);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error.Code);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Describe_WithUnknownCodeAndExpectedKind_ReturnsUnknownFallback ()
    {
        var service = new CodeCatalogService(CreateCatalog());
        var futureCode = "SOME_FUTURE_CODE";

        var result = service.Describe(
            new CodeCatalogCodeReference(futureCode, CodeCatalogKindValues.Error),
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
    public void Describe_WithUnknownCodeAndFutureExpectedKind_ReturnsUnknownFallback ()
    {
        var service = new CodeCatalogService(CreateCatalog());
        var futureCode = "SOME_FUTURE_CODE";

        var result = service.Describe(
            new CodeCatalogCodeReference(futureCode, "future-kind"),
            requireKnown: false);

        Assert.True(result.IsSuccess);
        Assert.False(result.Known);
        Assert.NotNull(result.Descriptor);
        Assert.Equal(futureCode, result.Descriptor!.Code);
        Assert.Equal(CodeCatalogKindValues.Unknown, result.Descriptor.Kind);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Describe_WithUnknownCodeAndRequireKnownTrue_ReturnsInvalidArgument ()
    {
        var service = new CodeCatalogService(CreateCatalog());

        var result = service.Describe(new CodeCatalogCodeReference("SOME_FUTURE_CODE", ExpectedKind: null), requireKnown: true);

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
        var descriptor = CreateDescriptor("DUPLICATE_CODE");
        var duplicateDescriptor = descriptor with
        {
            Summary = "Duplicate descriptor for test.",
        };

        Assert.Throws<InvalidOperationException>(() => new CodeCatalogModel(
            [
                new StubContributor([descriptor, duplicateDescriptor]),
            ]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithUnsupportedKind_Throws ()
    {
        var descriptor = CreateDescriptor("UNSUPPORTED_KIND_CODE") with
        {
            Kind = "unknown-kind",
        };

        Assert.Throws<InvalidOperationException>(() => new CodeCatalogModel(
            [
                new StubContributor([descriptor]),
            ]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithInvalidAppliesToCommand_Throws ()
    {
        var descriptor = CreateDescriptor("INVALID_COMMAND_CODE") with
        {
            AppliesTo = [new UcliCommand("unknown.command")],
        };

        Assert.Throws<InvalidOperationException>(() => new CodeCatalogModel(
            [
                new StubContributor([descriptor]),
            ]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithEmptyAppearsIn_Throws ()
    {
        var descriptor = CreateDescriptor("EMPTY_APPEARS_IN_CODE") with
        {
            AppearsIn = [],
        };

        Assert.Throws<InvalidOperationException>(() => new CodeCatalogModel(
            [
                new StubContributor([descriptor]),
            ]));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithUnknownRelatedCode_Throws ()
    {
        var descriptor = CreateDescriptor("UNKNOWN_RELATED_CODE") with
        {
            RelatedCodes = ["MISSING_RELATED_CODE"],
        };

        Assert.Throws<InvalidOperationException>(() => new CodeCatalogModel(
            [
                new StubContributor([descriptor]),
            ]));
    }

    private static void AssertInvalidArgument (CodeCatalogListResult result)
    {
        Assert.False(result.IsSuccess);
        Assert.Null(result.Descriptors);
        Assert.NotNull(result.Error);
        Assert.Equal(ExecutionErrorKind.InvalidArgument, result.Error!.Kind);
        Assert.Equal(UcliCoreErrorCodes.InvalidArgument, result.Error.Code);
    }

    private static CodeCatalogModel CreateCatalog ()
    {
        return new CodeCatalogModel(
            [
                new ContractsCodeCatalogContributor(),
                new ApplicationCodeCatalogContributor(),
            ]);
    }

    private static CodeCatalogDescriptor CreateDescriptor (string code)
    {
        return new CodeCatalogDescriptor(
            Code: code,
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

    private sealed class StubContributor : ICodeCatalogContributor
    {
        private readonly IReadOnlyList<CodeCatalogDescriptor> descriptors;

        public StubContributor (IReadOnlyList<CodeCatalogDescriptor> descriptors)
        {
            this.descriptors = descriptors;
        }

        public IReadOnlyList<CodeCatalogDescriptor> GetDescriptors ()
        {
            return descriptors;
        }
    }
}
