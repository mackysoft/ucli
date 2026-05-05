using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.OperationMetadata;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Context.Project;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Shared.Execution.Process;
using MackySoft.Ucli.UnityIntegration.Indexing.ReadIndex;

namespace MackySoft.Ucli.Tests;

public sealed class RequestPreparationServiceTests
{
    private const string RequestId = "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62";

    [Fact]
    [Trait("Size", "Small")]
    public void Parse_WhenDependenciesSucceed_ReturnsParsedRequestWithoutResolvingProject ()
    {
        const string requestJson = """{"steps":[]}""";
        var parsedRequest = CreateRequest();
        var parser = new SpyValidateRequestJsonParser(
            ValidateRequestJsonParseResult.Success(parsedRequest));
        var projectContextResolver = new SpyProjectContextResolver(
            ProjectContextResolutionResult.Success(CreateProjectContext()));
        var service = CreateService(parser, projectContextResolver);

        var result = service.Parse(requestJson);

        var normalizedRequestJson = CreateNormalizedRequestJson();
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParsedRequest);
        Assert.Equal(normalizedRequestJson, result.ParsedRequest!.RequestJson);
        Assert.Same(parsedRequest, result.ParsedRequest.Request);
        Assert.Equal(normalizedRequestJson, parser.ReceivedRequestJson);
        Assert.Equal(0, projectContextResolver.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenDependenciesSucceed_ReturnsPreparedRequest ()
    {
        const string requestJson = """{"steps":[]}""";
        var parsedRequest = CreateRequest();
        var projectContext = CreateProjectContext();
        var parser = new SpyValidateRequestJsonParser(
            ValidateRequestJsonParseResult.Success(parsedRequest));
        var projectContextResolver = new SpyProjectContextResolver(
            ProjectContextResolutionResult.Success(projectContext));
        var service = CreateService(parser, projectContextResolver);

        var result = await service.Prepare(
            projectPath: "/tmp/project",
            requestJson: requestJson,
            cancellationToken: CancellationToken.None);

        var normalizedRequestJson = CreateNormalizedRequestJson();
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PreparedRequest);
        Assert.Equal(normalizedRequestJson, result.PreparedRequest!.RequestJson);
        Assert.Same(parsedRequest, result.PreparedRequest.Request);
        Assert.Same(projectContext, result.PreparedRequest.ProjectContext);
        Assert.Equal(normalizedRequestJson, parser.ReceivedRequestJson);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenParseFails_ReturnsFailure ()
    {
        const string requestJson = """{"steps":[]}""";
        var error = ExecutionError.InvalidArgument("request JSON is invalid.");
        var service = CreateService(
            new SpyValidateRequestJsonParser(ValidateRequestJsonParseResult.Failure(error)),
            new SpyProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())));

        var result = await service.Prepare(
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
        const string requestJson = """{"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[]}""";
        var parser = new SpyValidateRequestJsonParser(ValidateRequestJsonParseResult.Success(CreateRequest()));
        var service = CreateService(
            parser,
            new SpyProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())));

        var result = await service.Prepare(
            projectPath: "/tmp/project",
            requestJson: requestJson,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("requestId", result.Error!.Message, StringComparison.Ordinal);
        Assert.Null(parser.ReceivedRequestJson);
        Assert.Null(result.PreparedRequest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenUserRequestStepsPropertyIsMissing_ReturnsFailureWithoutParsingOrResolvingProject ()
    {
        const string requestJson = """{}""";
        var parser = new SpyValidateRequestJsonParser(ValidateRequestJsonParseResult.Success(CreateRequest()));
        var projectContextResolver = new SpyProjectContextResolver(
            ProjectContextResolutionResult.Success(CreateProjectContext()));
        var service = CreateService(
            parser,
            projectContextResolver);

        var result = await service.Prepare(
            projectPath: "/tmp/project",
            requestJson: requestJson,
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.NotNull(result.Error);
        Assert.Contains("steps", result.Error!.Message, StringComparison.Ordinal);
        Assert.Null(parser.ReceivedRequestJson);
        Assert.Equal(0, projectContextResolver.CallCount);
        Assert.Null(result.PreparedRequest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenProjectContextResolutionFails_ReturnsFailure ()
    {
        const string requestJson = """{"steps":[]}""";
        var error = ExecutionError.InvalidArgument("project path is invalid.");
        var service = CreateService(
            new SpyValidateRequestJsonParser(ValidateRequestJsonParseResult.Success(CreateRequest())),
            new SpyProjectContextResolver(ProjectContextResolutionResult.Failure(error)));

        var result = await service.Prepare(
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
        var parser = new SpyValidateRequestJsonParser(ValidateRequestJsonParseResult.Success(CreateRequest()));
        var projectContextResolver = new SpyProjectContextResolver(
            ProjectContextResolutionResult.Success(CreateProjectContext()));
        var service = CreateService(parser, projectContextResolver);

        var result = await service.Prepare(
            projectPath: "/tmp/project",
            requestJson: requestJson,
            cancellationToken: token);

        Assert.True(result.IsSuccess);
        Assert.Equal(token, projectContextResolver.ReceivedToken);
    }

    private static ValidateRequest CreateRequest ()
    {
        return new ValidateRequest(
            ProtocolVersion: 1,
            RequestId: RequestId,
            Steps: Array.Empty<ValidateRequestStep?>());
    }

    private static ProjectContext CreateProjectContext ()
    {
        return new ProjectContext(
            new ResolvedUnityProjectContext(
                UnityProjectRoot: "/tmp/project",
                RepositoryRoot: "/tmp/repository",
                ProjectFingerprint: "project-fingerprint",
                PathSource: UnityProjectPathSource.CommandOption),
            UcliConfig.CreateDefault(),
            ConfigSource.Default);
    }

    private static string CreateNormalizedRequestJson ()
    {
        return $$"""{"protocolVersion":1,"requestId":"{{RequestId}}","steps":[]}""";
    }

    private static RequestPreparationService CreateService (
        IValidateRequestJsonParser parser,
        IProjectContextResolver projectContextResolver)
    {
        return new RequestPreparationService(
            new UserRequestJsonNormalizer(new FixedRequestIdFactory(RequestId)),
            parser,
            projectContextResolver);
    }

    private sealed class SpyValidateRequestJsonParser : IValidateRequestJsonParser
    {
        private readonly ValidateRequestJsonParseResult result;

        public SpyValidateRequestJsonParser (ValidateRequestJsonParseResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public string? ReceivedRequestJson { get; private set; }

        public ValidateRequestJsonParseResult Parse (string requestJson)
        {
            ReceivedRequestJson = requestJson;
            return result;
        }
    }

    private sealed class FixedRequestIdFactory : IRequestIdFactory
    {
        private readonly string requestId;

        public FixedRequestIdFactory (string requestId)
        {
            this.requestId = requestId;
        }

        public string Create ()
        {
            return requestId;
        }
    }

    private sealed class SpyProjectContextResolver : IProjectContextResolver
    {
        private readonly ProjectContextResolutionResult result;

        public SpyProjectContextResolver (ProjectContextResolutionResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public CancellationToken ReceivedToken { get; private set; }

        public int CallCount { get; private set; }

        public ValueTask<ProjectContextResolutionResult> Resolve (
            string? projectPath,
            CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            CallCount++;
            return ValueTask.FromResult(result);
        }
    }
}
