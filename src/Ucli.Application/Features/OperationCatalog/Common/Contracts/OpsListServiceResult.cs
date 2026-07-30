namespace MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;

/// <summary> Represents one normalized <c>ops list</c> service result. </summary>
internal abstract record OpsListServiceResult
{
    private OpsListServiceResult ()
    {
    }

    /// <summary> Creates a successful service result. </summary>
    /// <param name="output"> The successful output. </param>
    /// <param name="message"> The success message. </param>
    /// <returns> The successful result. </returns>
    public static Succeeded Success (
        OpsListExecutionOutput output,
        string message)
    {
        return new Succeeded(output, message);
    }

    /// <summary> Creates a failed service result. </summary>
    /// <param name="failure"> The classified application failure. </param>
    /// <returns> The failed result. </returns>
    public static Failed Failure (ApplicationFailure failure)
    {
        return new Failed(failure);
    }

    /// <summary> Represents a successful <c>ops list</c> service result. </summary>
    internal sealed record Succeeded : OpsListServiceResult
    {
        public Succeeded (
            OpsListExecutionOutput output,
            string message)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            Message = message;
        }

        public OpsListExecutionOutput Output { get; }

        public string Message { get; }
    }

    /// <summary> Represents a failed <c>ops list</c> service result. </summary>
    internal sealed record Failed : OpsListServiceResult
    {
        public Failed (ApplicationFailure error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public ApplicationFailure Error { get; }
    }
}
