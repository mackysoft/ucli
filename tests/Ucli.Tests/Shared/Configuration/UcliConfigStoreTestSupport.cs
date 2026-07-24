namespace MackySoft.Ucli.Tests;

using MackySoft.FileSystem;
using MackySoft.Tests;
using MackySoft.Ucli.Application.Shared.Configuration;

internal static class UcliConfigStoreTestSupport
{
    private static readonly KeyValuePair<string, int?>[] DefaultIpcTimeouts =
    [
        new(UcliContractConstants.Config.IpcTimeoutCommandTest, UcliContractConstants.Config.IpcTimeoutDefaultTestMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandReady, UcliContractConstants.Config.IpcTimeoutDefaultReadyMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandCompile, UcliContractConstants.Config.IpcTimeoutDefaultCompileMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandBuildRun, UcliContractConstants.Config.IpcTimeoutDefaultBuildRunMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandVerify, UcliContractConstants.Config.IpcTimeoutDefaultVerifyMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandStatus, UcliContractConstants.Config.IpcTimeoutDefaultStatusMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandValidate, UcliContractConstants.Config.IpcTimeoutDefaultValidateMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandPlan, UcliContractConstants.Config.IpcTimeoutDefaultPlanMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandCall, UcliContractConstants.Config.IpcTimeoutDefaultCallMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandEval, UcliContractConstants.Config.IpcTimeoutDefaultEvalMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandResolve, UcliContractConstants.Config.IpcTimeoutDefaultResolveMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandQuery, UcliContractConstants.Config.IpcTimeoutDefaultQueryMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandRefresh, UcliContractConstants.Config.IpcTimeoutDefaultRefreshMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandOps, UcliContractConstants.Config.IpcTimeoutDefaultOpsMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandDaemonStart, UcliContractConstants.Config.IpcTimeoutDefaultDaemonStartMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandDaemonStop, UcliContractConstants.Config.IpcTimeoutDefaultDaemonStopMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandDaemonCleanup, UcliContractConstants.Config.IpcTimeoutDefaultDaemonCleanupMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandDaemonStatus, UcliContractConstants.Config.IpcTimeoutDefaultDaemonStatusMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandDaemonList, UcliContractConstants.Config.IpcTimeoutDefaultDaemonListMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandLogsDaemonRead, UcliContractConstants.Config.IpcTimeoutDefaultLogsDaemonMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandLogsUnityRead, UcliContractConstants.Config.IpcTimeoutDefaultLogsUnityMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandLogsUnityClear, UcliContractConstants.Config.IpcTimeoutDefaultLogsUnityClearMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandScreenshot, UcliContractConstants.Config.IpcTimeoutDefaultScreenshotMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandPlayStatus, UcliContractConstants.Config.IpcTimeoutDefaultPlayStatusMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandPlayEnter, UcliContractConstants.Config.IpcTimeoutDefaultPlayEnterMilliseconds),
        new(UcliContractConstants.Config.IpcTimeoutCommandPlayExit, UcliContractConstants.Config.IpcTimeoutDefaultPlayExitMilliseconds),
    ];

    internal static ProjectFixture CreateProject (string testCaseName)
    {
        var scope = TestDirectories.CreateTempScope("ucli-config-store", testCaseName);
        try
        {
            var unityProjectPath = UnityProjectTestFactory.CreateMinimalUnityProject(scope, "UnityProject");
            var store = new UcliConfigStore(UcliConfigCompiler.CreateDefault());
            return new ProjectFixture(scope, unityProjectPath, store);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    internal static UcliConfigDiagnostic AssertSingleDiagnostic (
        IReadOnlyList<UcliConfigDiagnostic> diagnostics,
        string expectedCode)
    {
        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(expectedCode, diagnostic.Code);
        return diagnostic;
    }

    internal static void AssertDiagnostic (
        IReadOnlyList<UcliConfigDiagnostic> diagnostics,
        string expectedCode,
        string expectedPropertyPath)
    {
        Assert.Contains(
            diagnostics,
            diagnostic => diagnostic.Code == expectedCode
                && diagnostic.PropertyPath == expectedPropertyPath);
    }

    internal static void AssertDefaultIpcTimeouts (IReadOnlyDictionary<string, int?> actual)
    {
        Assert.Equal(DefaultIpcTimeouts.Length, actual.Count);
        foreach (var expected in DefaultIpcTimeouts)
        {
            Assert.True(
                actual.TryGetValue(expected.Key, out var actualTimeout),
                $"Default IPC timeout entry '{expected.Key}' was not found.");
            Assert.Equal(expected.Value, actualTimeout);
        }
    }

    internal sealed class ProjectFixture : IDisposable
    {
        private readonly TestDirectoryScope scope;

        internal ProjectFixture (
            TestDirectoryScope scope,
            string unityProjectPath,
            UcliConfigStore store)
        {
            this.scope = scope ?? throw new ArgumentNullException(nameof(scope));
            UnityProjectPath = AbsolutePath.Parse(unityProjectPath);
            Store = store ?? throw new ArgumentNullException(nameof(store));
            ConfigPath = Store.GetConfigPath(UnityProjectPath);
        }

        internal AbsolutePath UnityProjectPath { get; }

        internal UcliConfigStore Store { get; }

        internal AbsolutePath ConfigPath { get; }

        internal void WriteConfigJson (string json)
        {
            var relativeConfigPath = Path.GetRelativePath(scope.FullPath, ConfigPath.Value);
            scope.WriteFile(relativeConfigPath, json);
        }

        public void Dispose ()
        {
            scope.Dispose();
        }
    }
}
