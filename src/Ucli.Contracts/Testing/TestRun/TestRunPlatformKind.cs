namespace MackySoft.Ucli.Contracts.Testing;

/// <summary> Defines supported Unity test-run platform kinds. </summary>
public enum TestRunPlatformKind
{
    /// <summary> EditMode test execution. </summary>
    EditMode = 0,

    /// <summary> PlayMode test execution on the current active target. </summary>
    PlayMode = 1,

    /// <summary> Player test execution on a specific Unity build target. </summary>
    Player = 2,
}
