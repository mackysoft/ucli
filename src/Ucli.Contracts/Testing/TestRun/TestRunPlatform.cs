namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Represents one Unity-equivalent test-run platform value. </summary>
public readonly record struct TestRunPlatform
{
    /// <summary> Gets the EditMode platform value. </summary>
    public static TestRunPlatform EditMode { get; } = new(TestRunPlatformKind.EditMode, null);

    /// <summary> Gets the PlayMode platform value. </summary>
    public static TestRunPlatform PlayMode { get; } = new(TestRunPlatformKind.PlayMode, null);

    /// <summary> Gets the platform kind. </summary>
    public TestRunPlatformKind Kind { get; }

    /// <summary> Gets the Unity BuildTarget literal when <see cref="Kind" /> is <see cref="TestRunPlatformKind.Player" />. </summary>
    public string? PlayerBuildTargetLiteral { get; }

    /// <summary> Gets a value indicating whether this platform is EditMode. </summary>
    public bool IsEditMode => Kind == TestRunPlatformKind.EditMode;

    /// <summary> Gets a value indicating whether this platform is PlayMode. </summary>
    public bool IsPlayMode => Kind == TestRunPlatformKind.PlayMode;

    /// <summary> Gets a value indicating whether this platform targets a specific Unity player BuildTarget. </summary>
    public bool IsPlayer => Kind == TestRunPlatformKind.Player;

    private TestRunPlatform (
        TestRunPlatformKind kind,
        string? playerBuildTargetLiteral)
    {
        if (kind == TestRunPlatformKind.Player && string.IsNullOrWhiteSpace(playerBuildTargetLiteral))
        {
            throw new ArgumentException("Player build target literal must not be empty.", nameof(playerBuildTargetLiteral));
        }

        if (kind != TestRunPlatformKind.Player && playerBuildTargetLiteral is not null)
        {
            throw new ArgumentException("Player build target literal is only supported for player platforms.", nameof(playerBuildTargetLiteral));
        }

        Kind = kind;
        PlayerBuildTargetLiteral = playerBuildTargetLiteral;
    }

    /// <summary> Creates one player-target platform value. </summary>
    /// <param name="buildTargetLiteral"> The Unity BuildTarget literal. </param>
    /// <returns> The player-target platform value. </returns>
    public static TestRunPlatform Player (string buildTargetLiteral)
    {
        return new TestRunPlatform(TestRunPlatformKind.Player, buildTargetLiteral);
    }
}
