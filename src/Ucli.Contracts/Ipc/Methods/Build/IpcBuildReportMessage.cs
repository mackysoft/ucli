using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents one normalized BuildReport message. </summary>
public sealed record IpcBuildReportMessage
{
    /// <summary> Initializes one normalized BuildReport message. </summary>
    /// <param name="Type"> The non-empty normalized message type. </param>
    /// <param name="Content"> The message content, which may be empty but must not be <see langword="null" />. </param>
    /// <exception cref="ArgumentNullException"> Thrown when a required string is <see langword="null" />. </exception>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="Type" /> is empty. </exception>
    [JsonConstructor]
    public IpcBuildReportMessage (
        string Type,
        string Content)
    {
        this.Type = ContractArgumentGuard.RequireValue(Type, nameof(Type));
        this.Content = ContractArgumentGuard.RequireNotNull(Content, nameof(Content));
    }

    public string Type { get; }

    public string Content { get; }
}
