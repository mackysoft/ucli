namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Represents normalized Unity test platform values. </summary>
internal enum TestRunPlatform
{
    /// <summary> Represents an unknown or unsupported test platform value. </summary>
    Unknown = 0,

    /// <summary> Represents Unity EditMode tests. </summary>
    EditMode = 1,

    /// <summary> Represents Unity PlayMode tests. </summary>
    PlayMode = 2,
}