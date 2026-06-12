using MackySoft.Ucli.Application.Features.Assurance.Build.Payload;
using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Contracts;

/// <summary> Represents the application result of one build assurance command. </summary>
internal sealed record BuildExecutionResult
{
    private const string SuccessMessage = "Build completed.";

    private BuildExecutionResult (
        BuildExecutionOutput? output,
        ProjectIdentityInfo? project,
        IReadOnlyList<ApplicationFailure> errors,
        IpcBuildDirtyState? dirtyState)
    {
        Output = output;
        Project = project;
        Errors = errors;
        DirtyState = dirtyState;
    }

    /// <summary> Gets the output payload when execution reached verifier completion. </summary>
    public BuildExecutionOutput? Output { get; }

    /// <summary> Gets the resolved project when available. </summary>
    public ProjectIdentityInfo? Project { get; }

    /// <summary> Gets command failure errors. </summary>
    public IReadOnlyList<ApplicationFailure> Errors { get; }

    /// <summary> Gets the dirty state attached to a precondition command failure. </summary>
    public IpcBuildDirtyState? DirtyState { get; }

    /// <summary> Gets a value indicating whether command execution reached verifier completion. </summary>
    public bool IsSuccess => Output != null && Errors.Count == 0;

    /// <summary> Gets the user-facing command message. </summary>
    public string Message => IsSuccess ? SuccessMessage : Errors.FirstOrDefault()?.Message ?? "Build failed.";

    /// <summary> Creates a completed build execution result. </summary>
    public static BuildExecutionResult Success (BuildExecutionOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new BuildExecutionResult(output, output.Project, [], null);
    }

    /// <summary> Creates a failed build command result. </summary>
    public static BuildExecutionResult Failure (
        ExecutionError error,
        ProjectIdentityInfo? project = null,
        IpcBuildDirtyState? dirtyState = null)
    {
        ArgumentNullException.ThrowIfNull(error);
        return Failure(ApplicationFailure.FromExecutionError(error), project, dirtyState);
    }

    /// <summary> Creates a failed build command result. </summary>
    public static BuildExecutionResult Failure (
        ApplicationFailure failure,
        ProjectIdentityInfo? project = null,
        IpcBuildDirtyState? dirtyState = null)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new BuildExecutionResult(null, project, [failure], dirtyState);
    }
}
