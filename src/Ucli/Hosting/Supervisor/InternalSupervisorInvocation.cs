using MackySoft.FileSystem;

namespace MackySoft.Ucli.Hosting.Supervisor;

/// <summary> Represents the hidden supervisor-host invocation shape parsed from process arguments. </summary>
internal sealed class InternalSupervisorInvocation
{
    /// <summary> Gets the invocation value used when the public CLI path should run. </summary>
    public static InternalSupervisorInvocation NotMatched { get; } = new(false, false, null);

    /// <summary> Gets the invocation value used when hidden supervisor arguments are malformed. </summary>
    public static InternalSupervisorInvocation Invalid { get; } = new(true, false, null);

    private InternalSupervisorInvocation (
        bool isMatched,
        bool isValid,
        AbsolutePath? repositoryRoot)
    {
        IsMatched = isMatched;
        IsValid = isValid;
        RepositoryRoot = repositoryRoot;
    }

    /// <summary> Gets a value indicating whether the arguments target the internal supervisor host. </summary>
    public bool IsMatched { get; }

    /// <summary> Gets a value indicating whether the matched hidden invocation contains a usable repository root. </summary>
    public bool IsValid { get; }

    /// <summary> Gets the guarded repository root, or <see langword="null" /> when the hidden invocation is invalid. </summary>
    public AbsolutePath? RepositoryRoot { get; }

    /// <summary> Creates a valid hidden supervisor invocation. </summary>
    /// <param name="repositoryRoot"> The guarded repository root argument. </param>
    /// <returns> The valid invocation value. </returns>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="repositoryRoot" /> is <see langword="null" />. </exception>
    public static InternalSupervisorInvocation Valid (AbsolutePath repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(repositoryRoot);

        return new InternalSupervisorInvocation(true, true, repositoryRoot);
    }
}
