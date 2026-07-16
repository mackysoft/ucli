using System.Text.Json.Serialization;

namespace MackySoft.Ucli.Contracts.Ipc;

/// <summary> Represents a Play Mode transition IPC response payload. </summary>
/// <param name="Transition"> The transition result details. </param>
public sealed record IpcPlayTransitionResponse
{
    /// <summary> Initializes a Play Mode transition response. </summary>
    /// <param name="Transition"> The validated transition result. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="Transition" /> is <see langword="null" />. </exception>
    [JsonConstructor]
    public IpcPlayTransitionResponse (IpcPlayTransitionResult Transition)
    {
        this.Transition = Transition ?? throw new ArgumentNullException(nameof(Transition));
    }

    /// <summary> Gets the validated transition result. </summary>
    public IpcPlayTransitionResult Transition { get; }
}
