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

public sealed class RequestStaticValidationServiceTests
{
    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenCatalogLoadSucceeds_DelegatesToPureValidator ()
    {
        UcliOperationDescriptor[] operations =
        [
            new UcliOperationDescriptor(
                Name: "ucli.scene.open",
                Kind: UcliOperationKind.Query,
                Policy: OperationPolicy.Safe,
                ArgsSchemaJson: """{"type":"object"}"""),
        ];
        var pureValidator = new SpyRequestStaticValidator(ValidationResult.Success());
        var service = new RequestStaticValidationService(
            new StubOperationCatalog(operations),
            pureValidator);
        var projectContext = CreateProjectContext();
        var request = CreateRequest();
        var token = new CancellationTokenSource().Token;

        var result = await service.Validate(request, projectContext, token);

        Assert.True(result.IsValid);
        Assert.Equal(token, pureValidator.ReceivedToken);
        Assert.Same(request, pureValidator.ReceivedRequest);
        Assert.Same(projectContext.Config, pureValidator.ReceivedConfig);
        Assert.True(pureValidator.ReceivedCatalog!.IsAvailable);
        Assert.Single(pureValidator.ReceivedCatalog.Operations);
        Assert.Equal("ucli.scene.open", pureValidator.ReceivedCatalog.Operations[0].Name);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenCatalogLoadThrows_ReturnsFailureResult ()
    {
        var service = new RequestStaticValidationService(
            new ThrowingOperationCatalog(),
            new SpyRequestStaticValidator(ValidationResult.Success()));

        var result = await service.Validate(
            CreateRequest(),
            CreateProjectContext(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Empty(result.Errors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.InternalError, error.Kind);
        Assert.Contains("operation metadata", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenCatalogLoadThrowsTypedFailure_PreservesErrorKind ()
    {
        var service = new RequestStaticValidationService(
            new TypedFailingOperationCatalog(new OperationCatalogLoadException(
                ExecutionError.Timeout("Timed out before operation metadata discovery could begin."))),
            new SpyRequestStaticValidator(ValidationResult.Success()));

        var result = await service.Validate(
            CreateRequest(),
            CreateProjectContext(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Empty(result.Errors);
        var error = Assert.IsType<ExecutionError>(result.Error);
        Assert.Equal(ExecutionErrorKind.Timeout, error.Kind);
        Assert.Contains("Static validation could not load operation metadata.", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Validate_WhenPureValidatorReturnsError_PropagatesResult ()
    {
        var error = ExecutionError.InternalError("could not validate args.");
        var service = new RequestStaticValidationService(
            new StubOperationCatalog(
            [
                new UcliOperationDescriptor(
                    Name: "ucli.scene.open",
                    Kind: UcliOperationKind.Query,
                    Policy: OperationPolicy.Safe,
                    ArgsSchemaJson: "{ invalid-schema"),
            ]),
            new SpyRequestStaticValidator(ValidationResult.Failure(error)));

        var result = await service.Validate(
            CreateRequest(),
            CreateProjectContext(),
            CancellationToken.None);

        Assert.False(result.IsValid);
        Assert.Same(error, result.Error);
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

    private sealed class StubOperationCatalog : IOperationCatalog
    {
        private readonly IReadOnlyList<UcliOperationDescriptor> operations;

        public StubOperationCatalog (IReadOnlyList<UcliOperationDescriptor> operations)
        {
            this.operations = operations ?? throw new ArgumentNullException(nameof(operations));
        }

        public ValueTask<UcliOperationDescriptor?> Get (string name, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult<UcliOperationDescriptor?>(null);
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(operations);
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (
            ResolvedUnityProjectContext unityProject,
            UcliConfig config,
            UnityExecutionMode mode = UnityExecutionMode.Auto,
            TimeSpan? timeout = null,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(unityProject);
            ArgumentNullException.ThrowIfNull(config);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(operations);
        }
    }

    private sealed class ThrowingOperationCatalog : IOperationCatalog
    {
        public ValueTask<UcliOperationDescriptor?> Get (string name, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (
            ResolvedUnityProjectContext unityProject,
            UcliConfig config,
            UnityExecutionMode mode = UnityExecutionMode.Auto,
            TimeSpan? timeout = null,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("catalog discovery failed");
        }
    }

    private sealed class TypedFailingOperationCatalog : IOperationCatalog
    {
        private readonly Exception exception;

        public TypedFailingOperationCatalog (Exception exception)
        {
            this.exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        public ValueTask<UcliOperationDescriptor?> Get (string name, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IReadOnlyList<UcliOperationDescriptor>> GetAll (
            ResolvedUnityProjectContext unityProject,
            UcliConfig config,
            UnityExecutionMode mode = UnityExecutionMode.Auto,
            TimeSpan? timeout = null,
            bool failFast = false,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw exception;
        }
    }

    private sealed class SpyRequestStaticValidator : IRequestStaticValidator
    {
        private readonly ValidationResult result;

        public SpyRequestStaticValidator (ValidationResult result)
        {
            this.result = result ?? throw new ArgumentNullException(nameof(result));
        }

        public CancellationToken ReceivedToken { get; private set; }

        public ValidateRequest? ReceivedRequest { get; private set; }

        public RequestStaticValidationCatalog? ReceivedCatalog { get; private set; }

        public UcliConfig? ReceivedConfig { get; private set; }

        public ValueTask<ValidationResult> Validate (
            ValidateRequest request,
            RequestStaticValidationCatalog catalog,
            UcliConfig config,
            CancellationToken cancellationToken = default)
        {
            ReceivedToken = cancellationToken;
            ReceivedRequest = request;
            ReceivedCatalog = catalog;
            ReceivedConfig = config;
            return ValueTask.FromResult(result);
        }
    }
}
