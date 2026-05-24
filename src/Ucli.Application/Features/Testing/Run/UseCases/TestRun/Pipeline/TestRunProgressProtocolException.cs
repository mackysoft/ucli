namespace MackySoft.Ucli.Application.Features.Testing.Run.UseCases.TestRun.Pipeline;

/// <summary> Represents a violation of the Unity test-run progress stream contract. </summary>
internal sealed class TestRunProgressProtocolException : Exception
{
    /// <summary> Initializes a new instance of the <see cref="TestRunProgressProtocolException" /> class. </summary>
    /// <param name="message"> The violation message. </param>
    public TestRunProgressProtocolException (string message)
        : base(message)
    {
    }
}
