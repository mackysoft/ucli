namespace MackySoft.Ucli.Hosting.Supervisor;

/// <summary> Represents the hidden supervisor-host invocation shape parsed from process arguments. </summary>
/// <param name="IsMatched"> Whether the arguments target the internal supervisor host. </param>
/// <param name="IsValid"> Whether the matched hidden invocation contains a usable repository root. </param>
/// <param name="RepositoryRoot"> The parsed repository root, or an empty value when the hidden invocation is invalid. </param>
internal sealed record InternalSupervisorInvocation (
    bool IsMatched,
    bool IsValid,
    string RepositoryRoot)
{
    /// <summary> Gets the invocation value used when the public CLI path should run. </summary>
    public static InternalSupervisorInvocation NotMatched { get; } = new(false, false, string.Empty);

    /// <summary> Gets the invocation value used when hidden supervisor arguments are malformed. </summary>
    public static InternalSupervisorInvocation Invalid { get; } = new(true, false, string.Empty);

    /// <summary> Creates a valid hidden supervisor invocation. </summary>
    /// <param name="repositoryRoot"> The validated repository root argument. </param>
    /// <returns> The valid invocation value. </returns>
    /// <exception cref="ArgumentException"> Thrown when <paramref name="repositoryRoot" /> is empty or whitespace. </exception>
    public static InternalSupervisorInvocation Valid (string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
        {
            throw new ArgumentException("Repository root must not be empty.", nameof(repositoryRoot));
        }

        return new InternalSupervisorInvocation(true, true, repositoryRoot);
    }
}
