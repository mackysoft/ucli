using MackySoft.Ucli.Application.Shared.Execution.Progress;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;

/// <summary> Executes build assurance workflows. </summary>
internal interface IBuildService
{
    /// <summary> Executes one build assurance run. </summary>
    ValueTask<BuildExecutionResult> ExecuteAsync (
        BuildCommandInput input,
        ICommandProgressSink? progressSink = null,
        CancellationToken cancellationToken = default);
}
