namespace MackySoft.Ucli.Application.Features.Programs.Resolution;

/// <summary> Reads a request document selected from an already validated Program reference root. </summary>
internal interface IProgramDefinitionFileReader
{
    /// <summary> Reads a UTF-8 JSON document from an absolute path. </summary>
    ValueTask<ProgramDefinitionFileReadResult> ReadAsync (string path, CancellationToken cancellationToken = default);
}

/// <summary> Represents one Program request-document file read. </summary>
internal sealed record ProgramDefinitionFileReadResult (byte[]? Content, string? Error)
{
    /// <summary> Gets whether the read succeeded. </summary>
    public bool IsSuccess => Content is not null && Error is null;

    /// <summary> Creates a successful read result. </summary>
    public static ProgramDefinitionFileReadResult Success (byte[] content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return new ProgramDefinitionFileReadResult(content, null);
    }

    /// <summary> Creates a failed read result. </summary>
    public static ProgramDefinitionFileReadResult Failure (string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(error);
        return new ProgramDefinitionFileReadResult(null, error);
    }
}
