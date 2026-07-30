namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents one normalized internal <c>ops describe</c> read result. </summary>
internal abstract record OpsDescribeReadResult
{
    private OpsDescribeReadResult ()
    {
    }

    /// <summary> Creates a successful describe-read result. </summary>
    public static Succeeded Success (
        OpsDescribeReadOutput output,
        string message)
    {
        return new Succeeded(output, message);
    }

    /// <summary> Creates a failed describe-read result. </summary>
    public static Failed Failure (ApplicationFailure failure)
    {
        return new Failed(failure);
    }

    /// <summary> Represents a successful describe read. </summary>
    internal sealed record Succeeded : OpsDescribeReadResult
    {
        public Succeeded (
            OpsDescribeReadOutput output,
            string message)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            Message = message;
        }

        public OpsDescribeReadOutput Output { get; }

        public string Message { get; }
    }

    /// <summary> Represents a failed describe read. </summary>
    internal sealed record Failed : OpsDescribeReadResult
    {
        public Failed (ApplicationFailure error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public ApplicationFailure Error { get; }
    }
}
