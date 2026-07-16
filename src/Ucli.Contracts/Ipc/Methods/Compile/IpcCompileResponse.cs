using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>compile</c> IPC response payload. </summary>
public sealed record IpcCompileResponse
{
    /// <summary> Initializes a compile response from its completed summary. </summary>
    /// <param name="Summary"> The completed compile summary. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Summary" /> is <see langword="null" />. </exception>
    [JsonConstructor]
    public IpcCompileResponse (IpcCompileSummary Summary)
    {
        this.Summary = Summary ?? throw new ArgumentNullException(nameof(Summary));
    }

    /// <summary> Gets the completed compile summary. </summary>
    public IpcCompileSummary Summary { get; }
}
