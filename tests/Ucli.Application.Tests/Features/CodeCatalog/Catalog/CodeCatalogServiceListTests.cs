using MackySoft.Ucli.Application.Features.CodeCatalog.Catalog;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Application.Tests.Features.CodeCatalog.Catalog;

public sealed class CodeCatalogServiceListTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void List_WithoutFilters_ReturnsDescriptorsOrderedByCode ()
    {
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.List(new CodeCatalogListInput(Kind: null, Command: null));

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
    public void List_WithKindFilter_ReturnsExactKindMatches ()
    {
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.List(new CodeCatalogListInput(
            Kind: TextVocabulary.GetText(CodeCatalogKind.Error),
            Command: null));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        Assert.NotEmpty(result.Descriptors);
        Assert.All(result.Descriptors!, static descriptor => Assert.Equal(CodeCatalogKind.Error, descriptor.Kind));
        Assert.Contains(IpcTransportErrorCodes.IpcTimeout.Value, result.Descriptors!.Select(static descriptor => descriptor.Code.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithUnknownKindFilter_ReturnsEmptyDescriptors ()
    {
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.List(new CodeCatalogListInput(Kind: "unknown-kind", Command: null));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        Assert.Empty(result.Descriptors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithCommandLeafFilter_ReturnsFamilyMatches ()
    {
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.List(new CodeCatalogListInput(Kind: null, Command: "query.assets.find"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        Assert.Contains(IpcTransportErrorCodes.IpcTimeout.Value, result.Descriptors!.Select(static descriptor => descriptor.Code.Value));
        Assert.Contains(UcliCoreErrorCodes.InvalidArgument.Value, result.Descriptors.Select(static descriptor => descriptor.Code.Value));
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithEvalCommandFilter_ReturnsEvalExecutionErrors ()
    {
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.List(new CodeCatalogListInput(
            Kind: TextVocabulary.GetText(CodeCatalogKind.Error),
            Command: UcliCommandIds.Eval.Name));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        var codes = result.Descriptors!
            .Select(static descriptor => descriptor.Code)
            .ToArray();
        Assert.Contains(OperationAuthorizationErrorCodes.OperationNotAllowed, codes);
        Assert.Contains(IpcTransportErrorCodes.IpcTimeout, codes);
        Assert.Contains(EditorLifecycleErrorCodes.EditorCompiling, codes);
        Assert.Contains(ExecuteRequestErrorCodes.OperationContractViolation, codes);
        Assert.Contains(UnityExecutionModeDecisionErrorCodes.DaemonNotRunning, codes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithPlayCommandFilter_ReturnsPlayModeLifecycleErrors ()
    {
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.List(new CodeCatalogListInput(
            Kind: TextVocabulary.GetText(CodeCatalogKind.Error),
            Command: "play"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        var codes = result.Descriptors!
            .Select(static descriptor => descriptor.Code)
            .ToArray();
        Assert.Contains(PlayModeErrorCodes.PlayModeSessionNotAvailable, codes);
        Assert.Contains(PlayModeErrorCodes.PlayModeTransitionTimeout, codes);
        Assert.Contains(PlayModeErrorCodes.PlayModeTransitionBlocked, codes);
        Assert.Contains(PlayModeErrorCodes.PlayModeAlreadyChanging, codes);
        Assert.Contains(PlayModeErrorCodes.PlayModeEnterRejected, codes);
        Assert.Contains(PlayModeErrorCodes.PlayModeExitRejected, codes);
        Assert.Contains(PlayModeErrorCodes.PlayModeStateUnknown, codes);
        Assert.Contains(PlayModeErrorCodes.PlayModeRequiresGuiEditor, codes);
        Assert.DoesNotContain(PlayModeErrorCodes.PlayModeNotActive, codes);
        Assert.DoesNotContain(PlayModeErrorCodes.PlayModePersistenceForbidden, codes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithEveryDescriptorCommandFilter_ReturnsOwningDescriptor ()
    {
        var catalog = CodeCatalogTestSupport.CreateCoreCatalog();
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
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.List(new CodeCatalogListInput(Kind: null, Command: "query.unknown"));

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Descriptors);
        Assert.Empty(result.Descriptors);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithInvalidCommandFilter_ReturnsInvalidArgument ()
    {
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.List(new CodeCatalogListInput(Kind: null, Command: "query assets"));

        CodeCatalogTestSupport.AssertInvalidArgument(result);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void List_WithEmptyKindFilter_ReturnsInvalidArgument ()
    {
        var service = CodeCatalogTestSupport.CreateService();

        var result = service.List(new CodeCatalogListInput(Kind: " ", Command: null));

        CodeCatalogTestSupport.AssertInvalidArgument(result);
    }
}
