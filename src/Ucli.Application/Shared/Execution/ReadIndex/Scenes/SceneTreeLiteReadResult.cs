namespace MackySoft.Ucli.Application.Shared.Execution.ReadIndex.Scenes;

/// <summary> Represents one scene-tree-lite read result. </summary>
internal sealed record SceneTreeLiteReadResult
{
    private SceneTreeLiteReadResult (
        SceneTreeLiteReadOutput? output,
        string message,
        UcliCode? errorCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (output is null)
        {
            ArgumentNullException.ThrowIfNull(errorCode);
        }
        else if (errorCode is not null)
        {
            throw new ArgumentException("Successful read must not contain an error code.", nameof(errorCode));
        }

        Output = output;
        Message = message;
        ErrorCode = errorCode;
    }

    public SceneTreeLiteReadOutput? Output { get; }

    public string Message { get; }

    public UcliCode? ErrorCode { get; }

    /// <summary> Gets a value indicating whether the read succeeded. </summary>
    public bool IsSuccess => Output is not null;

    /// <summary> Creates a successful scene-tree-lite read result. </summary>
    public static SceneTreeLiteReadResult Success (
        SceneTreeLiteReadOutput output,
        string message)
    {
        ArgumentNullException.ThrowIfNull(output);
        return new SceneTreeLiteReadResult(output, message, null);
    }

    /// <summary> Creates a failed scene-tree-lite read result. </summary>
    public static SceneTreeLiteReadResult Failure (
        string message,
        UcliCode errorCode)
    {
        return new SceneTreeLiteReadResult(null, message, errorCode);
    }
}
