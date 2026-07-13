using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>compile</c> IPC response payload. </summary>
public sealed record IpcCompileResponse
{
    /// <summary> Initializes a compile response for one non-empty run identifier. </summary>
    /// <param name="RunId"> The compile run identifier. </param>
    /// <param name="Summary"> The completed compile summary. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Summary" /> is <see langword="null" />. </exception>
    [JsonConstructor]
    public IpcCompileResponse (
        Guid RunId,
        IpcCompileSummary Summary)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
        this.Summary = Summary ?? throw new ArgumentNullException(nameof(Summary));
    }

    /// <summary> Gets the non-empty compile run identifier. </summary>
    public Guid RunId { get; }

    /// <summary> Gets the completed compile summary. </summary>
    public IpcCompileSummary Summary { get; }
}
