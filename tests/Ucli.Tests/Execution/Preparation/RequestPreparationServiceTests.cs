using MackySoft.Ucli.Cli.Requests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts.Configuration;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.Foundation;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.ReadIndex;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Tests;

public sealed class RequestPreparationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task ReadAndParse_WhenDependenciesSucceed_ReturnsParsedRequestWithoutResolvingProject ()
    {
        const string requestJson = """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[]}""";
        var parsedRequest = CreateRequest();
        var inputReader = new StubRequestInputReader(
            RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput));
        var parser = new SpyValidateRequestJsonParser(
            ValidateRequestJsonParseResult.Success(parsedRequest));
        var projectContextResolver = new SpyProjectContextResolver(
            ProjectContextResolutionResult.Success(CreateProjectContext()));
        var service = new RequestPreparationService(inputReader, parser, projectContextResolver);

        var result = await service.ReadAndParse(
            requestPath: "request.json",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.ParsedRequest);
        Assert.Equal(requestJson, result.ParsedRequest!.RequestJson);
        Assert.Equal(RequestInputSource.StandardInput, result.ParsedRequest.InputSource);
        Assert.Same(parsedRequest, result.ParsedRequest.Request);
        Assert.Equal(requestJson, parser.ReceivedRequestJson);
        Assert.Equal(0, projectContextResolver.CallCount);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenDependenciesSucceed_ReturnsPreparedRequest ()
    {
        const string requestJson = """{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[]}""";
        var parsedRequest = CreateRequest();
        var projectContext = CreateProjectContext();
        var inputReader = new StubRequestInputReader(
            RequestInputReadResult.Success(requestJson, RequestInputSource.StandardInput));
        var parser = new SpyValidateRequestJsonParser(
            ValidateRequestJsonParseResult.Success(parsedRequest));
        var projectContextResolver = new SpyProjectContextResolver(
            ProjectContextResolutionResult.Success(projectContext));
        var service = new RequestPreparationService(inputReader, parser, projectContextResolver);

        var result = await service.Prepare(
            requestPath: "request.json",
            projectPath: "/tmp/project",
            cancellationToken: CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.PreparedRequest);
        Assert.Equal(requestJson, result.PreparedRequest!.RequestJson);
        Assert.Equal(RequestInputSource.StandardInput, result.PreparedRequest.InputSource);
        Assert.Same(parsedRequest, result.PreparedRequest.Request);
        Assert.Same(projectContext, result.PreparedRequest.ProjectContext);
        Assert.Equal(requestJson, parser.ReceivedRequestJson);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenInputReadFails_ReturnsFailure ()
    {
        var error = ExecutionError.InvalidArgument("request input is invalid.");
        var service = new RequestPreparationService(
            new StubRequestInputReader(RequestInputReadResult.Failure(error)),
            new SpyValidateRequestJsonParser(ValidateRequestJsonParseResult.Success(CreateRequest())),
            new SpyProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())));

        var result = await service.Prepare(
            requestPath: null,
            projectPath: "/tmp/project",
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Null(result.PreparedRequest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenParseFails_ReturnsFailure ()
    {
        var error = ExecutionError.InvalidArgument("request JSON is invalid.");
        var service = new RequestPreparationService(
            new StubRequestInputReader(RequestInputReadResult.Success("""{"protocolVersion":1}""", RequestInputSource.StandardInput)),
            new SpyValidateRequestJsonParser(ValidateRequestJsonParseResult.Failure(error)),
            new SpyProjectContextResolver(ProjectContextResolutionResult.Success(CreateProjectContext())));

        var result = await service.Prepare(
            requestPath: null,
            projectPath: "/tmp/project",
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Null(result.PreparedRequest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_WhenProjectContextResolutionFails_ReturnsFailure ()
    {
        var error = ExecutionError.InvalidArgument("project path is invalid.");
        var service = new RequestPreparationService(
            new StubRequestInputReader(RequestInputReadResult.Success("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[]}""", RequestInputSource.StandardInput)),
            new SpyValidateRequestJsonParser(ValidateRequestJsonParseResult.Success(CreateRequest())),
            new SpyProjectContextResolver(ProjectContextResolutionResult.Failure(error)));

        var result = await service.Prepare(
            requestPath: null,
            projectPath: "/tmp/project",
            cancellationToken: CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Same(error, result.Error);
        Assert.Null(result.PreparedRequest);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Prepare_PropagatesCancellationTokenToDependencies ()
    {
        var token = new CancellationTokenSource().Token;
        var inputReader = new SpyRequestInputReader(
            RequestInputReadResult.Success("""{"protocolVersion":1,"requestId":"9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62","steps":[]}""", RequestInputSource.StandardInput));
        var parser = new SpyValidateRequestJsonParser(ValidateRequestJsonParseResult.Success(CreateRequest()));
        var projectContextResolver = new SpyProjectContextResolver(
            ProjectContextResolutionResult.Success(CreateProjectContext()));
        var service = new RequestPreparationService(inputReader, parser, projectContextResolver);

        var result = await service.Prepare(
            requestPath: null,
            projectPath: "/tmp/project",
            cancellationToken: token);

        Assert.True(result.IsSuccess);
        Assert.Equal(token, inputReader.ReceivedToken);
        Assert.Equal(token, projectContextResolver.ReceivedToken);
    }

    private static ValidateRequest CreateRequest ()
    {
        return new ValidateRequest(
            ProtocolVersion: 1,
            RequestId: "9b0e6d1e-3f55-4a6b-8c66-5b9a3a7c9c62",
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

    private sealed class StubRequestInputReader : IRequestInputReader
    {
        private readonly RequestInputReadResult result;

        public StubRequestInputReader (RequestInputReadResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public ValueTask<RequestInputReadResult> ReadAsync (
            string? requestPath,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class SpyRequestInputReader : IRequestInputReader
    {
        private readonly RequestInputReadResult result;

        public SpyRequestInputReader (RequestInputReadResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public CancellationToken ReceivedToken { get; private set; }

        public ValueTask<RequestInputReadResult> ReadAsync (
            string? requestPath,
            CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            return ValueTask.FromResult(result);
        }
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