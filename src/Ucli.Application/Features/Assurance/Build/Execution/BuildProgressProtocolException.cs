namespace MackySoft.Ucli.Application.Features.Assurance.Build.Execution;

/// <summary> Represents a violation of the Unity build progress stream contract. </summary>
internal sealed class BuildProgressProtocolException : Exception
{
    /// <summary> Initializes a new instance of the <see cref="BuildProgressProtocolException" /> class. </summary>
    /// <param name="message"> The violation message. </param>
    public BuildProgressProtocolException (string message)
        : base(message)
    {
    }
}
