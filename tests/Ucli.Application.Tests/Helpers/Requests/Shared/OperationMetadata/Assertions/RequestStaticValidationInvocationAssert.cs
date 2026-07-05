using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Contracts.Configuration;

namespace MackySoft.Ucli.Application.Tests;

internal static class RequestStaticValidationInvocationAssert
{
    public static RecordingRequestStaticValidationPreflightService.Invocation ReadIndexPreflightPreparedOnce (
        RecordingRequestStaticValidationPreflightService preflightService,
        PreparedRequestContext expectedPreparedRequest,
        ReadIndexMode? expectedReadIndexMode)
    {
        var invocation = Assert.Single(preflightService.Invocations);
        Assert.Same(expectedPreparedRequest, invocation.PreparedRequest);
        Assert.Equal(expectedReadIndexMode, invocation.ReadIndexMode);
        return invocation;
    }

    public static RecordingReadIndexValidationCatalogResolver.Invocation ReadIndexCatalogResolvedForPreparedProject (
        RecordingReadIndexValidationCatalogResolver resolver,
        PreparedRequestContext expectedPreparedRequest,
        ReadIndexMode expectedReadIndexMode)
    {
        var invocation = Assert.Single(resolver.Invocations);
        Assert.Same(expectedPreparedRequest.ProjectContext.UnityProject, invocation.UnityProject);
        Assert.Equal(expectedReadIndexMode, invocation.ReadIndexMode);
        return invocation;
    }

    public static RecordingRequestStaticValidator.Invocation PureStaticValidationRequestedOnce (
        RecordingRequestStaticValidator validator,
        bool expectedCatalogAvailable)
    {
        var invocation = Assert.Single(validator.Invocations);
        Assert.Equal(expectedCatalogAvailable, invocation.Catalog.IsAvailable);
        return invocation;
    }

    public static void MetadataResolutionFailureReturnedBeforeStaticValidation (
        RequestStaticValidationPreflightResult result,
        PreparedRequestContext expectedPreparedRequest,
        ReadIndexInfo expectedReadIndex,
        UcliCode expectedErrorCode,
        string expectedMessageFragment,
        RecordingRequestStaticValidator validator)
    {
        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.NotNull(result.Error);
        Assert.Contains(expectedMessageFragment, result.Error!.Message, StringComparison.Ordinal);
        Assert.Equal(expectedErrorCode, result.ErrorCode);
        Assert.Same(expectedPreparedRequest, result.PreparedRequest);
        Assert.Same(expectedReadIndex, result.ReadIndex);
        Assert.Empty(result.ValidationErrors);
        Assert.Empty(validator.Invocations);
    }

    public static RecordingRequestStaticValidator.Invocation PureStaticValidationReceivedAvailableOperationCatalog (
        RecordingRequestStaticValidator validator,
        PreparedRequestContext expectedPreparedRequest,
        string expectedOperationName)
    {
        var invocation = PureStaticValidationRequestedOnce(
            validator,
            expectedCatalogAvailable: true);
        Assert.Same(expectedPreparedRequest.Request, invocation.Request);
        Assert.Same(expectedPreparedRequest.ProjectContext.Config, invocation.Config);
        Assert.Contains(invocation.Catalog.Operations, operation => operation.Name == expectedOperationName);
        return invocation;
    }

    public static RecordingRequestStaticValidator.Invocation PureStaticValidationReceivedAvailableOperationCatalog (
        RecordingRequestStaticValidator validator,
        ValidateRequest expectedRequest,
        UcliConfig expectedConfig,
        CancellationToken expectedCancellationToken,
        string expectedOperationName)
    {
        var invocation = PureStaticValidationRequestedOnce(
            validator,
            expectedCatalogAvailable: true);
        Assert.Same(expectedRequest, invocation.Request);
        Assert.Same(expectedConfig, invocation.Config);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Contains(invocation.Catalog.Operations, operation => operation.Name == expectedOperationName);
        return invocation;
    }
}
