using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

/// <summary> Represents merged and normalized configuration values before project and editor resolution. </summary>
/// <param name="ProjectPath"> The absolute Unity project path candidate. </param>
/// <param name="Mode"> The execution-mode value. </param>
/// <param name="UnityVersion"> The optional Unity version override value. </param>
/// <param name="UnityEditorPath"> The optional Unity editor path override value. </param>
/// <param name="TestPlatform"> The parsed test-platform value. </param>
/// <param name="RawTestPlatform"> The raw merged test-platform value. </param>
/// <param name="TestFilter"> The optional test-filter value. </param>
/// <param name="TestCategories"> The normalized test-category values. </param>
/// <param name="AssemblyNames"> The normalized assembly-name values. </param>
/// <param name="TestSettingsPath"> The optional test-settings path value. </param>
/// <param name="TimeoutMilliseconds"> The optional timeout value in milliseconds from CLI/profile input. </param>
internal sealed record MergedTestRunConfiguration (
    string ProjectPath,
    UnityExecutionMode Mode,
    string? UnityVersion,
    string? UnityEditorPath,
    TestRunPlatform? TestPlatform,
    string RawTestPlatform,
    string? TestFilter,
    string[] TestCategories,
    string[] AssemblyNames,
    string? TestSettingsPath,
    int? TimeoutMilliseconds);
