using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Features.Testing.Profiles;

/// <summary> Represents JSON values used by <c>ucli test profile init</c>. </summary>
/// <param name="SchemaVersion"> The profile schema version. </param>
/// <param name="ProjectPath"> The Unity project path value. </param>
/// <param name="UnityVersion"> The optional Unity version. </param>
/// <param name="UnityEditorPath"> The optional Unity editor executable path. </param>
/// <param name="TestPlatform"> The default test platform value. </param>
/// <param name="TestFilter"> The optional test filter value. </param>
/// <param name="TestCategories"> The test-category list. </param>
/// <param name="AssemblyNames"> The assembly-name list. </param>
/// <param name="TestSettingsPath"> The optional test settings path. </param>
/// <param name="Timeout"> The timeout value in milliseconds. </param>
internal sealed record TestProfile (
    int SchemaVersion,
    string ProjectPath,
    string? UnityVersion,
    string? UnityEditorPath,
    string TestPlatform,
    string? TestFilter,
    string[] TestCategories,
    string[] AssemblyNames,
    string? TestSettingsPath,
    int Timeout)
{
    private const int CurrentSchemaVersion = 1;
    private const string DefaultProjectPath = ".";
    private const int DefaultTimeoutMilliseconds = 1800000;

    /// <summary> Creates default values defined by the test-command specification. </summary>
    /// <returns> The default profile values. </returns>
    public static TestProfile CreateDefault ()
    {
        return new TestProfile(
            SchemaVersion: CurrentSchemaVersion,
            ProjectPath: DefaultProjectPath,
            UnityVersion: null,
            UnityEditorPath: null,
            TestPlatform: TestRunPlatformCodec.EditMode,
            TestFilter: null,
            TestCategories: [],
            AssemblyNames: [],
            TestSettingsPath: null,
            Timeout: DefaultTimeoutMilliseconds);
    }
}
