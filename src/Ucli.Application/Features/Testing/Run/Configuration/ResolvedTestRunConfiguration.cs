using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Testing;

namespace MackySoft.Ucli.Application.Features.Testing.Run.Configuration;

/// <summary> Represents fully resolved test-run configuration values. </summary>
/// <param name="UnityProject"> The resolved Unity project context. </param>
/// <param name="Mode"> The execution-mode value. </param>
/// <param name="UnityVersion"> The resolved Unity version. </param>
/// <param name="UnityEditorPath"> The resolved Unity editor executable path. </param>
/// <param name="TestPlatform"> The parsed test-platform value. </param>
/// <param name="TestFilter"> The optional test-filter value. </param>
/// <param name="TestCategories"> The normalized test-category values. </param>
/// <param name="AssemblyNames"> The normalized assembly-name values. </param>
/// <param name="TimeoutMilliseconds"> The optional timeout value in milliseconds from CLI/profile input. </param>
internal sealed record ResolvedTestRunConfiguration (
    ResolvedUnityProjectContext UnityProject,
    UnityExecutionMode Mode,
    string UnityVersion,
    AbsolutePath UnityEditorPath,
    TestRunPlatform TestPlatform,
    string? TestFilter,
    string[] TestCategories,
    string[] AssemblyNames,
    int? TimeoutMilliseconds);
