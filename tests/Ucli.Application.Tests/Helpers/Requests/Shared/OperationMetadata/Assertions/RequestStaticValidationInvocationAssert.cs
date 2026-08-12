using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Shared.Execution.ErrorCodes;
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
        Assert.Equal(expectedPreparedRequest, invocation.PreparedRequest);
        Assert.Equal(expectedReadIndexMode, invocation.ReadIndexMode);
        return invocation;
    }

    public static RecordingReadIndexValidationCatalogResolver.Invocation ReadIndexCatalogResolvedForPreparedProject (
        RecordingReadIndexValidationCatalogResolver resolver,
        PreparedRequestContext expectedPreparedRequest,
        ReadIndexMode expectedReadIndexMode)
    {
        var invocation = Assert.Single(resolver.Invocations);
        Assert.Equal(expectedPreparedRequest.ProjectContext.UnityProject, invocation.UnityProject);
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
        RecordingRequestStaticValidator validator)
    {
        Assert.False(result.IsSuccess);
        Assert.False(result.HasValidationErrors);
        Assert.NotNull(result.Error);
        Assert.Equal(expectedErrorCode, result.Error.Code);
        Assert.Equal(expectedPreparedRequest, result.PreparedRequest);
        Assert.Equal(expectedReadIndex, result.ReadIndex);
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
        Assert.Equal(expectedPreparedRequest.Request, invocation.Request);
        Assert.Equal(expectedPreparedRequest.ProjectContext.Config, invocation.Config);
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
        Assert.Equal(expectedRequest, invocation.Request);
        Assert.Equal(expectedConfig, invocation.Config);
        Assert.Equal(expectedCancellationToken, invocation.CancellationToken);
        Assert.Contains(invocation.Catalog.Operations, operation => operation.Name == expectedOperationName);
        return invocation;
    }
}
