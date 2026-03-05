using MackySoft.Ucli.Configuration;
using MackySoft.Ucli.Context;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Execution;
using MackySoft.Ucli.UnityProject.Resolution;

namespace MackySoft.Ucli.Status;

/// <summary> Resolves status workflow preflight values from project context, timeout option, and Unity version. </summary>
internal sealed class StatusExecutionContextResolver : IStatusExecutionContextResolver
{
    private readonly IInitStatusContextResolver initStatusContextResolver;

    private readonly IUnityVersionResolver unityVersionResolver;

    /// <summary> Initializes a new instance of the <see cref="StatusExecutionContextResolver" /> class. </summary>
    /// <param name="initStatusContextResolver"> The init/status context resolver dependency. </param>
    /// <param name="unityVersionResolver"> The Unity version resolver dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public StatusExecutionContextResolver (
        IInitStatusContextResolver initStatusContextResolver,
        IUnityVersionResolver unityVersionResolver)
    {
        this.initStatusContextResolver = initStatusContextResolver ?? throw new ArgumentNullException(nameof(initStatusContextResolver));
        this.unityVersionResolver = unityVersionResolver ?? throw new ArgumentNullException(nameof(unityVersionResolver));
    }

    /// <summary> Resolves context, timeout, and Unity version values for one status execution. </summary>
    /// <param name="projectPath"> The optional <c>--projectPath</c> value. </param>
    /// <param name="timeout"> The optional <c>--timeout</c> value in milliseconds. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the execution-context resolution result. </returns>
    public async ValueTask<StatusExecutionContextResolutionResult> Resolve (
        string? projectPath,
        string? timeout,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var contextResolutionResult = await initStatusContextResolver.Resolve(projectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResolutionResult.IsSuccess)
        {
            return StatusExecutionContextResolutionResult.Failure(contextResolutionResult.Error!);
        }

        var context = contextResolutionResult.Context!;
        var timeoutResolutionResult = IpcCommandTimeoutResolver.Resolve(timeout, UcliCommandIds.Status, context.Config);
        if (!timeoutResolutionResult.IsSuccess)
        {
            return StatusExecutionContextResolutionResult.Failure(timeoutResolutionResult.Error!);
        }

        var unityVersionResolutionResult = unityVersionResolver.Resolve(
            context.UnityProject.UnityProjectRoot,
            preferredUnityVersion: null);
        if (!unityVersionResolutionResult.IsSuccess)
        {
            return StatusExecutionContextResolutionResult.Failure(unityVersionResolutionResult.Error!);
        }

        var executionContext = new StatusExecutionContext(
            Context: context,
            Timeout: timeoutResolutionResult.Timeout!.Value,
            UnityVersion: unityVersionResolutionResult.UnityVersion!);
        return StatusExecutionContextResolutionResult.Success(executionContext);
    }
}