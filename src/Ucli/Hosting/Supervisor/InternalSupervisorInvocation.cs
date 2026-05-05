namespace MackySoft.Ucli.Hosting.Supervisor;

/// <summary> Represents the hidden supervisor-host invocation shape parsed from process arguments. </summary>
/// <param name="IsMatched"> Whether the arguments target the internal supervisor host. </param>
/// <param name="RepositoryRoot"> The parsed repository root, or an empty value when the hidden invocation is invalid. </param>
internal sealed record InternalSupervisorInvocation (
    bool IsMatched,
    string RepositoryRoot)
{
    /// <summary> Gets the invocation value used when the public CLI path should run. </summary>
    public static InternalSupervisorInvocation NotMatched { get; } = new(false, string.Empty);
}
