namespace MackySoft.Ucli.Features.Testing.Run.Configuration;

/// <summary> Represents JSON values loaded from one test-run profile file. </summary>
internal sealed record TestRunProfile
{
    /// <summary> Gets the supported profile schema version. </summary>
    public const int SchemaVersionValue = 1;

    /// <summary> Gets the profile schema version. </summary>
    public int SchemaVersion { get; init; } = SchemaVersionValue;

    /// <summary> Gets the Unity project path value. </summary>
    public string? ProjectPath { get; init; }

    /// <summary> Gets the optional Unity version override value. </summary>
    public string? UnityVersion { get; init; }

    /// <summary> Gets the optional Unity editor path override value. </summary>
    public string? UnityEditorPath { get; init; }

    /// <summary> Gets the optional test-platform value. </summary>
    public string? TestPlatform { get; init; }

    /// <summary> Gets the optional build target value. </summary>
    public string? BuildTarget { get; init; }

    /// <summary> Gets the optional test-filter value. </summary>
    public string? TestFilter { get; init; }

    /// <summary> Gets the optional test-category values. </summary>
    public string[]? TestCategories { get; init; }

    /// <summary> Gets the optional assembly-name values. </summary>
    public string[]? AssemblyNames { get; init; }

    /// <summary> Gets the optional <c>TestSettings.json</c> path value. </summary>
    public string? TestSettingsPath { get; init; }

    /// <summary> Gets the optional timeout-milliseconds value. </summary>
    public int? Timeout { get; init; }
}