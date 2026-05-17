namespace MackySoft.Ucli.Contracts;

/// <summary> Defines read-index error code values. </summary>
public static class ReadIndexErrorCodes
{
    /// <summary> Gets the error code emitted when read-index bootstrap cannot be completed. </summary>
    public static readonly UcliCode ReadIndexBootstrapFailed = new("READ_INDEX_BOOTSTRAP_FAILED");

    /// <summary> Gets the error code emitted when read-index files are malformed. </summary>
    public static readonly UcliCode ReadIndexFormatInvalid = new("READ_INDEX_FORMAT_INVALID");

    /// <summary> Gets the error code emitted when a request requires fresh read-index but freshness is not <c>fresh</c>. </summary>
    public static readonly UcliCode ReadIndexFreshRequired = new("READ_INDEX_FRESH_REQUIRED");
}
