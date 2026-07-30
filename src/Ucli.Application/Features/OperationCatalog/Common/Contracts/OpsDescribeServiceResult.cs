namespace MackySoft.Ucli.Application.Features.OperationCatalog.Common.Contracts;

/// <summary> Represents one normalized <c>ops describe</c> service result. </summary>
internal abstract record OpsDescribeServiceResult
{
    private OpsDescribeServiceResult ()
    {
    }

    /// <summary> Creates a successful service result. </summary>
    /// <param name="output"> The successful output. </param>
    /// <param name="message"> The success message. </param>
    /// <returns> The successful result. </returns>
    public static Succeeded Success (
        OpsDescribeExecutionOutput output,
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

    /// <summary> Represents a successful <c>ops describe</c> service result. </summary>
    internal sealed record Succeeded : OpsDescribeServiceResult
    {
        public Succeeded (
            OpsDescribeExecutionOutput output,
            string message)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            Message = message;
        }

        public OpsDescribeExecutionOutput Output { get; }

        public string Message { get; }
    }

    /// <summary> Represents a failed <c>ops describe</c> service result. </summary>
    internal sealed record Failed : OpsDescribeServiceResult
    {
        public Failed (ApplicationFailure error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public ApplicationFailure Error { get; }
    }
}
