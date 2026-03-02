namespace MackySoft.Ucli.TestRun.Configuration;

/// <summary> Represents merged and normalized configuration values before project and editor resolution. </summary>
/// <param name="ProjectPath"> The absolute Unity project path candidate. </param>
/// <param name="Mode"> The raw execution-mode option value. </param>
/// <param name="UnityVersion"> The optional Unity version override value. </param>
/// <param name="UnityEditorPath"> The optional Unity editor path override value. </param>
/// <param name="TestPlatform"> The parsed test-platform value. </param>
/// <param name="RawTestPlatform"> The raw merged test-platform value. </param>
/// <param name="BuildTarget"> The optional build target value. </param>
/// <param name="TestFilter"> The optional test-filter value. </param>
/// <param name="TestCategories"> The normalized test-category values. </param>
/// <param name="AssemblyNames"> The normalized assembly-name values. </param>
/// <param name="TestSettingsPath"> The optional test-settings path value. </param>
/// <param name="TimeoutSeconds"> The timeout value in seconds. </param>
internal sealed record MergedTestRunConfiguration (
    string ProjectPath,
    string Mode,
    string? UnityVersion,
    string? UnityEditorPath,
    TestRunPlatform TestPlatform,
    string RawTestPlatform,
    string? BuildTarget,
    string? TestFilter,
    string[] TestCategories,
    string[] AssemblyNames,
    string? TestSettingsPath,
    int TimeoutSeconds);