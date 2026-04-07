namespace MackySoft.Ucli.TestRun;

/// <summary> Represents raw CLI input values for test-run execution. </summary>
/// <param name="ProjectPath"> Optional Unity project root path. </param>
/// <param name="ProfilePath"> Optional profile configuration path. </param>
/// <param name="Mode"> Optional Unity execution mode option value (<c>auto|daemon|oneshot</c>). </param>
/// <param name="UnityVersion"> Optional Unity version override. </param>
/// <param name="UnityEditorPath"> Optional Unity editor executable path override. </param>
/// <param name="TestPlatform"> Optional test platform option value (<c>editmode|playmode</c>). </param>
/// <param name="BuildTarget"> Optional Unity build target for PlayMode tests. </param>
/// <param name="TestFilter"> Optional Unity test filter pattern. </param>
/// <param name="TestCategory"> Optional test categories parsed from the CLI option. </param>
/// <param name="AssemblyName"> Optional assembly names parsed from the CLI option. </param>
/// <param name="TestSettingsPath"> Optional path to <c>TestSettings.json</c>. </param>
/// <param name="TimeoutMilliseconds"> Optional timeout in milliseconds. </param>
/// <param name="FailFast"> Whether daemon-backed execution should fail immediately instead of waiting for lifecycle readiness. </param>
internal sealed record TestRunCommandInput (
    string? ProjectPath,
    string? ProfilePath,
    string? Mode,
    string? UnityVersion,
    string? UnityEditorPath,
    string? TestPlatform,
    string? BuildTarget,
    string? TestFilter,
    string[]? TestCategory,
    string[]? AssemblyName,
    string? TestSettingsPath,
    int? TimeoutMilliseconds,
    bool FailFast = false);