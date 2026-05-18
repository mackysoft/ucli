using MackySoft.Ucli.Application.Shared.Foundation;
using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Application.Features.Assurance.Compile.Artifacts;

/// <summary> Represents the result of reading one compile summary artifact. </summary>
internal sealed record CompileRunArtifactReadResult
{
    private CompileRunArtifactReadResult (
        IpcCompileSummary? summary,
        ExecutionError? error,
        bool isMissing)
    {
        Summary = summary;
        Error = error;
        IsMissing = isMissing;
    }

    /// <summary> Gets the parsed summary when available. </summary>
    public IpcCompileSummary? Summary { get; }

    /// <summary> Gets the read error when the artifact exists but cannot be trusted. </summary>
    public ExecutionError? Error { get; }

    /// <summary> Gets whether the summary artifact does not exist yet. </summary>
    public bool IsMissing { get; }

    /// <summary> Gets a value indicating whether the summary was read successfully. </summary>
    public bool IsSuccess => Summary != null && Error == null && !IsMissing;

    /// <summary> Creates a successful read result. </summary>
    public static CompileRunArtifactReadResult Success (IpcCompileSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return new CompileRunArtifactReadResult(summary, null, isMissing: false);
    }

    /// <summary> Creates a missing-artifact result. </summary>
    public static CompileRunArtifactReadResult Missing ()
    {
        return new CompileRunArtifactReadResult(null, null, isMissing: true);
    }

    /// <summary> Creates a failed read result. </summary>
    public static CompileRunArtifactReadResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new CompileRunArtifactReadResult(null, error, isMissing: false);
    }
}
