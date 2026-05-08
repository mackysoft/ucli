using MackySoft.Ucli.Application.Shared.Foundation;

namespace MackySoft.Ucli.Hosting.Cli.Options;

/// <summary> Represents one normalization result for the <c>--editorMode</c> option. </summary>
internal sealed record DaemonEditorModeOptionNormalizationResult (
    DaemonEditorMode? EditorMode,
    ExecutionError? Error)
{
    /// <summary> Gets a value indicating whether the option was normalized successfully. </summary>
    public bool IsSuccess => Error == null;

    /// <summary> Creates a successful normalization result. </summary>
    /// <param name="editorMode"> The normalized Editor mode override, or <see langword="null" /> when the option was omitted. </param>
    /// <returns> The successful result. </returns>
    public static DaemonEditorModeOptionNormalizationResult Success (DaemonEditorMode? editorMode)
    {
        return new DaemonEditorModeOptionNormalizationResult(editorMode, null);
    }

    /// <summary> Creates a failed normalization result. </summary>
    /// <param name="error"> The normalization error. </param>
    /// <returns> The failed result. </returns>
    public static DaemonEditorModeOptionNormalizationResult Failure (ExecutionError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new DaemonEditorModeOptionNormalizationResult(null, error);
    }
}
