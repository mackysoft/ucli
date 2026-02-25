namespace MackySoft.Ucli.Foundation
{
    /// <summary> Represents a structured error returned from foundation services. </summary>
    /// <param name="Kind"> The classification used to map the error to the CLI contract. </param>
    /// <param name="Message"> The user-facing error message. </param>
    internal sealed record ExecutionError (
        ExecutionErrorKind Kind,
        string Message);
}
