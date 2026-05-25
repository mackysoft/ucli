using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Represents normalized request values required for one Unity test run. </summary>
    internal sealed record UnityTestRunRequestContext (
        string RunId,
        string TestPlatform,
        TestMode TestMode,
        BuildTarget? TargetPlatform,
        string? TestFilter,
        string[] TestCategories,
        string[] AssemblyNames,
        string ResultsXmlPath,
        string EditorLogPath,
        string ConsoleLogPath);
}
