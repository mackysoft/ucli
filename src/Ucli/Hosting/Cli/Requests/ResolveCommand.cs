using ConsoleAppFramework;
using MackySoft.Ucli.Features.Requests.Resolve.UseCases.Resolve;
using MackySoft.Ucli.Hosting.Cli.Common.Contracts;
using MackySoft.Ucli.Hosting.Cli.Common.Execution;
using MackySoft.Ucli.Hosting.Cli.Options;

namespace MackySoft.Ucli.Hosting.Cli.Requests;

/// <summary> Provides the resolve CLI command entry point. </summary>
internal sealed class ResolveCommand
{
    private readonly IResolveService resolveService;

    /// <summary> Initializes a new instance of the ResolveCommand class. </summary>
    public ResolveCommand (IResolveService resolveService)
    {
        this.resolveService = resolveService ?? throw new ArgumentNullException(nameof(resolveService));
    }

    /// <summary> Executes the resolve command and emits the JSON result contract. </summary>
    /// <param name="projectPath">-p|--projectPath, Optional target Unity project path.</param>
    /// <param name="mode">Unity execution mode (auto|daemon|oneshot).</param>
    /// <param name="timeout">Timeout in milliseconds.</param>
    /// <param name="readIndexMode">--readIndexMode, readIndex mode (disabled|allowStale|requireFresh).</param>
    /// <param name="failFast">--failFast, Fails immediately when Unity editor lifecycle is not yet ready.</param>
    /// <param name="globalObjectId">--globalObjectId, GlobalObjectId selector.</param>
    /// <param name="assetGuid">--assetGuid, Asset GUID selector.</param>
    /// <param name="assetPath">--assetPath, Unity asset path selector.</param>
    /// <param name="projectAssetPath">--projectAssetPath, Project-relative asset path selector.</param>
    /// <param name="scene">Scene path used with hierarchyPath.</param>
    /// <param name="hierarchyPath">--hierarchyPath, GameObject hierarchy path used with scene or prefab.</param>
    /// <param name="componentType">--componentType, Optional scene component type selector.</param>
    /// <param name="prefab">Prefab path used with hierarchyPath.</param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> The exit code contained in the emitted command result. </returns>
    [Command(UcliCommandNames.Resolve)]
    public async Task<int> Resolve (
        string? projectPath = null,
        string? mode = null,
        string? timeout = null,
        string? readIndexMode = null,
        bool failFast = false,
        string? globalObjectId = null,
        string? assetGuid = null,
        string? assetPath = null,
        string? projectAssetPath = null,
        string? scene = null,
        string? hierarchyPath = null,
        string? componentType = null,
        string? prefab = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CommandExecutionState.MarkStarted();

        var selectorResult = ResolveSelectorInputFactory.Create(
            globalObjectId,
            assetGuid,
            assetPath,
            projectAssetPath,
            scene,
            hierarchyPath,
            componentType,
            prefab);
        if (!selectorResult.IsSuccess)
        {
            var errorResult = ResolveCommandResultFactory.CreateExecutionError(selectorResult.Error!);
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedReadIndexModeResult = ReadIndexModeOptionNormalizer.Normalize(readIndexMode);
        if (!normalizedReadIndexModeResult.IsSuccess)
        {
            var errorResult = ResolveCommandResultFactory.CreateExecutionError(normalizedReadIndexModeResult.Error!);
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedTimeoutResult = TimeoutOptionNormalizer.Normalize(timeout);
        if (!normalizedTimeoutResult.IsSuccess)
        {
            var errorResult = ResolveCommandResultFactory.CreateExecutionError(normalizedTimeoutResult.Error!);
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var normalizedModeResult = ExecutionModeOptionNormalizer.Normalize(mode);
        if (!normalizedModeResult.IsSuccess)
        {
            var errorResult = ResolveCommandResultFactory.CreateExecutionError(normalizedModeResult.Error!);
            CommandResultWriter.WriteToStandardOutput(errorResult);
            return errorResult.ExitCode;
        }

        var serviceResult = await resolveService.Execute(
                new ResolveCommandInput(
                    ProjectPath: projectPath,
                    Mode: normalizedModeResult.Mode,
                    TimeoutMilliseconds: normalizedTimeoutResult.TimeoutMilliseconds,
                    ReadIndexMode: normalizedReadIndexModeResult.Mode,
                    FailFast: failFast,
                    Selector: selectorResult.Selector!),
                cancellationToken)
            .ConfigureAwait(false);
        var commandResult = ResolveCommandResultFactory.Create(serviceResult);
        CommandResultWriter.WriteToStandardOutput(commandResult);
        return commandResult.ExitCode;
    }
}
