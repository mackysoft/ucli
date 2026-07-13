using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a <c>compile</c> IPC request payload. </summary>
public sealed record IpcCompileRequest
{
    /// <summary> Initializes a compile request for one non-empty run identifier. </summary>
    /// <param name="RunId"> The CLI-generated compile run identifier. </param>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="RunId" /> is empty. </exception>
    [JsonConstructor]
    public IpcCompileRequest (Guid RunId)
    {
        if (RunId == Guid.Empty)
        {
            throw new ArgumentException("Run id must not be empty.", nameof(RunId));
        }

        this.RunId = RunId;
    }

    /// <summary> Gets the non-empty compile run identifier. </summary>
    public Guid RunId { get; }

    /// <summary> Gets the request timeout budget propagated by the caller, in milliseconds. </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? TimeoutMilliseconds { get; init; }
}
