using MackySoft.Ucli.Cli.Requests;
using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Operations;
using MackySoft.Ucli.UnityProject;

namespace MackySoft.Ucli.Execution;

/// <summary> Executes request preflight for phase-based command execution. </summary>
internal sealed class PhaseExecutionPreflightService : IPhaseExecutionPreflightService
{
    private readonly IRequestInputReader requestInputReader;
    private readonly IValidateRequestJsonParser requestJsonParser;
    private readonly IUnityProjectResolver unityProjectResolver;
    private readonly IUcliConfigStore configStore;
    private readonly IRequestStaticValidator requestStaticValidator;

    /// <summary> Initializes a new instance of the <see cref="PhaseExecutionPreflightService" /> class. </summary>
    /// <param name="requestInputReader"> The request-input reader dependency. </param>
    /// <param name="requestJsonParser"> The request-json parser dependency. </param>
    /// <param name="unityProjectResolver"> The Unity project resolver dependency. </param>
    /// <param name="configStore"> The config store dependency. </param>
    /// <param name="requestStaticValidator"> The request static-validator dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when any dependency is <see langword="null" />. </exception>
    public PhaseExecutionPreflightService (
        IRequestInputReader requestInputReader,
        IValidateRequestJsonParser requestJsonParser,
        IUnityProjectResolver unityProjectResolver,
        IUcliConfigStore configStore,
        IRequestStaticValidator requestStaticValidator)
    {
        this.requestInputReader = requestInputReader ?? throw new ArgumentNullException(nameof(requestInputReader));
        this.requestJsonParser = requestJsonParser ?? throw new ArgumentNullException(nameof(requestJsonParser));
        this.unityProjectResolver = unityProjectResolver ?? throw new ArgumentNullException(nameof(unityProjectResolver));
        this.configStore = configStore ?? throw new ArgumentNullException(nameof(configStore));
        this.requestStaticValidator = requestStaticValidator ?? throw new ArgumentNullException(nameof(requestStaticValidator));
    }

    /// <summary> Executes preflight and returns a prepared request or structured errors. </summary>
    /// <param name="requestPath"> The optional request path from <c>--requestPath</c>. </param>
    /// <param name="projectPath"> The optional Unity project path from <c>--projectPath</c>. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The preflight result. </returns>
    public async ValueTask<PhaseExecutionPreflightResult> Prepare (
        string? requestPath,
        string? projectPath,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var inputReadResult = await requestInputReader.ReadAsync(requestPath, cancellationToken).ConfigureAwait(false);
        if (!inputReadResult.IsSuccess)
        {
            return PhaseExecutionPreflightResult.Failure(inputReadResult.Error!);
        }

        var requestJson = inputReadResult.Json!;
        var parseResult = requestJsonParser.Parse(requestJson);
        if (!parseResult.IsSuccess)
        {
            return PhaseExecutionPreflightResult.Failure(parseResult.Error!);
        }

        var unityProjectResult = unityProjectResolver.Resolve(projectPath);
        if (!unityProjectResult.IsSuccess)
        {
            return PhaseExecutionPreflightResult.Failure(unityProjectResult.Error!);
        }

        var unityProjectContext = unityProjectResult.Context!;
        var configLoadResult = await configStore.Load(unityProjectContext.RepositoryRoot, cancellationToken).ConfigureAwait(false);
        if (!configLoadResult.IsSuccess)
        {
            return PhaseExecutionPreflightResult.Failure(configLoadResult.Error!);
        }

        var request = parseResult.Request!;
        var config = configLoadResult.Config!;
        var validationResult = await requestStaticValidator.Validate(request, unityProjectContext, config, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return PhaseExecutionPreflightResult.ValidationFailure(validationResult.Errors);
        }

        var preparedRequest = new PhaseExecutionPreparedRequest(
            RequestJson: requestJson,
            InputSource: inputReadResult.Source!.Value,
            Request: request,
            UnityProject: unityProjectContext,
            Config: config,
            ConfigSource: configLoadResult.Source);
        return PhaseExecutionPreflightResult.Success(preparedRequest);
    }
}