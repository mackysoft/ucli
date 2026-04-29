using MackySoft.Ucli.Contracts.Testing;
using MackySoft.Ucli.Shared.Execution.UnityExecutionMode.Decision;

namespace MackySoft.Ucli.Features.Testing.Run.Configuration;

/// <summary> Represents configuration-resolution input values for one test-run request. </summary>
/// <param name="ProjectPath"> Optional Unity project root path. </param>
/// <param name="ProfilePath"> Optional profile configuration path. </param>
/// <param name="Mode"> Optional Unity execution-mode value. </param>
/// <param name="UnityVersion"> Optional Unity version override. </param>
/// <param name="UnityEditorPath"> Optional Unity editor executable path override. </param>
/// <param name="TestPlatform"> Optional test-platform value. </param>
/// <param name="TestFilter"> Optional Unity test filter pattern. </param>
/// <param name="TestCategory"> Optional test categories parsed from the CLI option. </param>
/// <param name="AssemblyName"> Optional assembly names parsed from the CLI option. </param>
/// <param name="TestSettingsPath"> Optional path to <c>TestSettings.json</c>. </param>
/// <param name="TimeoutMilliseconds"> Optional timeout in milliseconds. </param>
internal sealed record TestRunConfigurationRequest (
    string? ProjectPath,
    string? ProfilePath,
    UnityExecutionMode? Mode,
    string? UnityVersion,
    string? UnityEditorPath,
    TestRunPlatform? TestPlatform,
    string? TestFilter,
    string[]? TestCategory,
    string[]? AssemblyName,
    string? TestSettingsPath,
    int? TimeoutMilliseconds);
