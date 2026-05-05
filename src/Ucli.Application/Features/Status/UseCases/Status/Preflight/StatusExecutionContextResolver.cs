using MackySoft.Ucli.Application.Features.Requests.Shared.Execution;
using MackySoft.Ucli.Application.Features.Requests.Shared.Preparation;
using MackySoft.Ucli.Application.Features.Requests.Shared.Validation.Parsing;
using MackySoft.Ucli.Application.Features.Status.UseCases.Status;
using MackySoft.Ucli.Application.Shared.Configuration;
using MackySoft.Ucli.Application.Shared.Context;
using MackySoft.Ucli.Application.Shared.Execution.Lifecycle;
using MackySoft.Ucli.Application.Shared.Execution.Process;
using MackySoft.Ucli.Application.Shared.Execution.Timeout;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Decision;
using MackySoft.Ucli.Application.Shared.Execution.UnityExecutionMode.Probe;
using MackySoft.Ucli.Application.Shared.Unity.Resolution;
using MackySoft.Ucli.Contracts;

namespace MackySoft.Ucli.Application.Features.Status.UseCases.Status.Preflight;

/// <summary> Resolves status workflow preflight values from project context, timeout option, and Unity version. </summary>
internal sealed class StatusExecutionContextResolver : IStatusExecutionContextResolver
{
    private readonly IProjectContextResolver projectContextResolver;

    private readonly IUnityVersionResolver unityVersionResolver;

    /// <summary> Initializes a new instance of the <see cref="StatusExecutionContextResolver" /> class. </summary>
    /// <param name="projectContextResolver"> The shared project-context resolver dependency. </param>
    /// <param name="unityVersionResolver"> The Unity version resolver dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when one dependency is <see langword="null" />. </exception>
    public StatusExecutionContextResolver (
        IProjectContextResolver projectContextResolver,
        IUnityVersionResolver unityVersionResolver)
    {
        this.projectContextResolver = projectContextResolver ?? throw new ArgumentNullException(nameof(projectContextResolver));
        this.unityVersionResolver = unityVersionResolver ?? throw new ArgumentNullException(nameof(unityVersionResolver));
    }

    /// <summary> Resolves context, timeout, and Unity version values for one status execution. </summary>
    /// <param name="input"> The normalized status command input. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the execution-context resolution result. </returns>
    public async ValueTask<StatusExecutionContextResolutionResult> Resolve (
        StatusCommandInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        var contextResolutionResult = await projectContextResolver.Resolve(input.ProjectPath, cancellationToken).ConfigureAwait(false);
        if (!contextResolutionResult.IsSuccess)
        {
            return StatusExecutionContextResolutionResult.Failure(contextResolutionResult.Error!);
        }

        var context = contextResolutionResult.Context!;
        var timeoutResolutionResult = IpcCommandTimeoutResolver.ResolveNormalized(
            input.TimeoutMilliseconds,
            UcliCommandIds.Status,
            context.Config);
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
