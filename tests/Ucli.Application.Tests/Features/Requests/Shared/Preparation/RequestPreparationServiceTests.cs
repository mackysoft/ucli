using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Application.Tests;

public sealed class RequestPreparationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenDependenciesSucceed_ReturnsPreparedRequest ()
    {
        const string requestJson = """{"steps":[]}""";
        var parsedRequest = CreateRequest();
        var projectContext = ProjectContextTestFactory.CreateTemporaryFixtureProject();
        var parser = new RecordingValidateRequestJsonParser
        {
            Result = ValidateRequestJsonParseResult.Success(parsedRequest),
        };
        var projectContextResolver = new StaticProjectContextResolver(
            ProjectContextResolutionResult.Success(projectContext));
        var service = CreateService(parser, projectContextResolver);

        var result = await service.PrepareAsync(
            projectPath: "/tmp/project",
            requestJson: requestJson,
            cancellationToken: CancellationToken.None);

        var normalizedRequestJson = CreateNormalizedRequestJson();
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PreparedRequest);
        Assert.Equal(normalizedRequestJson, result.PreparedRequest!.RequestJson);
        Assert.Same(parsedRequest, result.PreparedRequest.Request);
        Assert.Same(projectContext, result.PreparedRequest.ProjectContext);
        RequestPreparationInvocationAssert.RequestJsonParsedOnce(parser, normalizedRequestJson);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenParseFails_ReturnsFailure ()
    {
        const string requestJson = """{"steps":[]}""";
        var error = ExecutionError.InvalidArgument("request JSON is invalid.");
        var service = CreateService(
            new RecordingValidateRequestJsonParser
            {
                Result = ValidateRequestJsonParseResult.Failure(error),
            },
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ProjectContextTestFactory.CreateTemporaryFixtureProject())));

        var result = await service.PrepareAsync(
            projectPath: "/tmp/project",
            requestJson: requestJson,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Null(result.PreparedRequest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenUserRequestNormalizationFails_ReturnsFailureWithoutParsing ()
    {
        const string requestJson = """{"unknown":true,"steps":[]}""";
        var error = ExecutionError.InvalidArgument("unknown property.");
        var parser = new RecordingValidateRequestJsonParser
        {
            Result = ValidateRequestJsonParseResult.Success(CreateRequest()),
        };
        var service = CreateService(
            new RecordingUserRequestJsonNormalizer
            {
                Result = UserRequestJsonNormalizationResult.Failure(error),
            },
            parser,
            new StaticProjectContextResolver(ProjectContextResolutionResult.Success(ProjectContextTestFactory.CreateTemporaryFixtureProject())));

        var result = await service.PrepareAsync(
            projectPath: "/tmp/project",
            requestJson: requestJson,
            cancellationToken: CancellationToken.None);

        RequestPreparationInvocationAssert.NormalizationFailureReturnedBeforeParsing(
            result,
            error,
            parser);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenUserRequestStepsPropertyIsMissing_ReturnsFailureWithoutParsingOrResolvingProject ()
    {
        const string requestJson = """{}""";
        var error = ExecutionError.InvalidArgument("steps property is missing.");
        var parser = new RecordingValidateRequestJsonParser
        {
            Result = ValidateRequestJsonParseResult.Success(CreateRequest()),
        };
        var projectContextResolver = new UnexpectedProjectContextResolver();
        var service = CreateService(
            new RecordingUserRequestJsonNormalizer
            {
                Result = UserRequestJsonNormalizationResult.Failure(error),
            },
            parser,
            projectContextResolver);

        var result = await service.PrepareAsync(
            projectPath: "/tmp/project",
            requestJson: requestJson,
            cancellationToken: CancellationToken.None);

        RequestPreparationInvocationAssert.NormalizationFailureReturnedBeforeParsing(
            result,
            error,
            parser);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenProjectContextResolutionFails_ReturnsFailure ()
    {
        const string requestJson = """{"steps":[]}""";
        var error = ExecutionError.InvalidArgument("project path is invalid.");
        var service = CreateService(
            new RecordingValidateRequestJsonParser
            {
                Result = ValidateRequestJsonParseResult.Success(CreateRequest()),
            },
            new StaticProjectContextResolver(ProjectContextResolutionResult.Failure(error)));

        var result = await service.PrepareAsync(
            projectPath: "/tmp/project",
            requestJson: requestJson,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Null(result.PreparedRequest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_PropagatesCancellationTokenToDependencies ()
    {
        const string requestJson = """{"steps":[]}""";
        var token = new CancellationTokenSource().Token;
        var parser = new RecordingValidateRequestJsonParser
        {
            Result = ValidateRequestJsonParseResult.Success(CreateRequest()),
        };
        var projectContextResolver = new StaticProjectContextResolver(
            ProjectContextResolutionResult.Success(ProjectContextTestFactory.CreateTemporaryFixtureProject()));
        var service = CreateService(parser, projectContextResolver);

        var result = await service.PrepareAsync(
            projectPath: "/tmp/project",
            requestJson: requestJson,
            cancellationToken: token);

        Assert.True(result.IsSuccess);
        ProjectContextResolverAssert.ProjectContextResolvedOnce(
            projectContextResolver,
            "/tmp/project",
            token);
    }

    private static ValidateRequest CreateRequest ()
    {
        return new ValidateRequest(
            ProtocolVersion: 1,
            Steps: Array.Empty<ValidateRequestStep?>());
    }

    private static string CreateNormalizedRequestJson ()
    {
        return """{"protocolVersion":1,"steps":[]}""";
    }

    private static RequestPreparationService CreateService (
        IValidateRequestJsonParser parser,
        IProjectContextResolver projectContextResolver)
    {
        return CreateService(
            new RecordingUserRequestJsonNormalizer
            {
                Result = UserRequestJsonNormalizationResult.Success(CreateNormalizedRequestJson()),
            },
            parser,
            projectContextResolver);
    }

    private static RequestPreparationService CreateService (
        IUserRequestJsonNormalizer userRequestJsonNormalizer,
        IValidateRequestJsonParser parser,
        IProjectContextResolver projectContextResolver)
    {
        return new RequestPreparationService(
            userRequestJsonNormalizer,
            parser,
            projectContextResolver);
    }

}
