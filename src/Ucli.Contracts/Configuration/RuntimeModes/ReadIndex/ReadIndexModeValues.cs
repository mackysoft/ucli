namespace MackySoft.Ucli.Contracts.Configuration;

/// <summary> Defines literal values for <c>readIndexDefaultMode</c> in <c>.ucli/config.json</c>. </summary>
public static class ReadIndexModeValues
{
    /// <summary> Gets the value that disables read-index usage. </summary>
    public const string Disabled = "disabled";

    /// <summary> Gets the value that allows stale/probable-fresh read-index usage. </summary>
    public const string AllowStale = "allowStale";

    /// <summary> Gets the value that requires fresh read-index usage. </summary>
    public const string RequireFresh = "requireFresh";
}
