using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.UnityIntegration.Project;

namespace MackySoft.Ucli.Features.Testing.Run.Configuration;

/// <summary> Represents fully resolved test-run configuration values. </summary>
/// <param name="UnityProject"> The resolved Unity project context. </param>
/// <param name="Mode"> The raw execution-mode option value. </param>
/// <param name="UnityVersion"> The resolved Unity version. </param>
/// <param name="UnityEditorPath"> The resolved Unity editor executable path. </param>
/// <param name="TestPlatform"> The parsed test-platform value. </param>
/// <param name="RawTestPlatform"> The raw merged test-platform value. </param>
/// <param name="BuildTarget"> The optional build target value. </param>
/// <param name="TestFilter"> The optional test-filter value. </param>
/// <param name="TestCategories"> The normalized test-category values. </param>
/// <param name="AssemblyNames"> The normalized assembly-name values. </param>
/// <param name="TestSettingsPath"> The optional test-settings path value. </param>
/// <param name="TimeoutMilliseconds"> The optional timeout value in milliseconds from CLI/profile input. </param>
internal sealed record ResolvedTestRunConfiguration (
    ResolvedUnityProjectContext UnityProject,
    string Mode,
    string UnityVersion,
    string UnityEditorPath,
    IpcTestRunPlatform TestPlatform,
    string RawTestPlatform,
    string? BuildTarget,
    string? TestFilter,
    string[] TestCategories,
    string[] AssemblyNames,
    string? TestSettingsPath,
    int? TimeoutMilliseconds);