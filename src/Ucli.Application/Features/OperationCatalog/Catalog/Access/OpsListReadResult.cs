namespace MackySoft.Ucli.Application.Features.OperationCatalog.Catalog.Access;

/// <summary> Represents one normalized internal <c>ops list</c> read result. </summary>
internal abstract record OpsListReadResult
{
    private OpsListReadResult ()
    {
    }

    /// <summary> Creates a successful list-read result. </summary>
    public static Succeeded Success (
        OpsListReadOutput output,
        string message)
    {
        return new Succeeded(output, message);
    }

    /// <summary> Creates a failed list-read result. </summary>
    public static Failed Failure (ApplicationFailure failure)
    {
        return new Failed(failure);
    }

    /// <summary> Represents a successful list read. </summary>
    internal sealed record Succeeded : OpsListReadResult
    {
        public Succeeded (
            OpsListReadOutput output,
            string message)
        {
            Output = output ?? throw new ArgumentNullException(nameof(output));
            ArgumentException.ThrowIfNullOrWhiteSpace(message);
            Message = message;
        }

        public OpsListReadOutput Output { get; }

        public string Message { get; }
    }

    /// <summary> Represents a failed list read. </summary>
    internal sealed record Failed : OpsListReadResult
    {
        public Failed (ApplicationFailure error)
        {
            Error = error ?? throw new ArgumentNullException(nameof(error));
        }

        public ApplicationFailure Error { get; }
    }
}
