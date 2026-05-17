namespace MackySoft.Ucli.Tests;

internal static class UcliContractConstants
{
    internal static class CliOption
    {
        public const string Force = "--force";

        public const string OutputPath = "--outputPath";

        public const string ProjectPath = "--projectPath";

        public const string Timeout = "--timeout";

        public const string Mode = "--mode";

        public const string TestPlatform = "--testPlatform";

        public const string FailFast = "--failFast";

        public const string EditorMode = "--editorMode";

        public const string OnStartupBlocked = "--onStartupBlocked";

        public const string ReadIndexMode = "--readIndexMode";

        public const string NameRegex = "--nameRegex";

        public const string Kind = "--kind";

        public const string MaxPolicy = "--maxPolicy";

        public const string PlanToken = "--planToken";

        public const string WithPlan = "--withPlan";

        public const string AllowDangerous = "--allowDangerous";

        public const string Unknown = "--unknown";
    }

    internal static class Config
    {
        public const int SchemaVersion = 1;

        public const string DefaultOperationAllowlistPattern = "^ucli\\.";

        public const string OperationPolicyDangerous = "dangerous";

        public const string OperationPolicySafe = "safe";

        public const string PlanTokenModeOptional = "optional";

        public const string PlanTokenModeRequired = "required";

        public const string ReadIndexModeDisabled = "disabled";

        public const string ReadIndexModeAllowStale = "allowStale";

        public const string ReadIndexModeRequireFresh = "requireFresh";

        public const int IpcDefaultTimeoutMilliseconds = 3000;

        public const int IpcTimeoutDefaultTestMilliseconds = 300000;

        public const int IpcTimeoutDefaultReadyMilliseconds = 10000;

        public const int IpcTimeoutDefaultStatusMilliseconds = 5000;

        public const int IpcTimeoutDefaultValidateMilliseconds = 10000;

        public const int IpcTimeoutDefaultPlanMilliseconds = 20000;

        public const int IpcTimeoutDefaultCallMilliseconds = 60000;

        public const int IpcTimeoutDefaultResolveMilliseconds = 10000;

        public const int IpcTimeoutDefaultQueryMilliseconds = 10000;

        public const int IpcTimeoutDefaultRefreshMilliseconds = 120000;

        public const int IpcTimeoutDefaultOpsMilliseconds = 120000;

        public const int IpcTimeoutDefaultDaemonStartMilliseconds = 60000;

        public const int IpcTimeoutDefaultDaemonStopMilliseconds = 10000;

        public const int IpcTimeoutDefaultDaemonCleanupMilliseconds = 3000;

        public const int IpcTimeoutDefaultDaemonStatusMilliseconds = 3000;

        public const int IpcTimeoutDefaultDaemonListMilliseconds = 3000;

        public const int IpcTimeoutDefaultLogsDaemonMilliseconds = 3000;

        public const int IpcTimeoutDefaultLogsUnityMilliseconds = 3000;

        public const int IpcTimeoutDefaultLogsUnityClearMilliseconds = 3000;

        public const string IpcTimeoutCommandTest = "test";

        public const string IpcTimeoutCommandReady = "ready";

        public const string IpcTimeoutCommandStatus = "status";

        public const string IpcTimeoutCommandValidate = "validate";

        public const string IpcTimeoutCommandPlan = "plan";

        public const string IpcTimeoutCommandCall = "call";

        public const string IpcTimeoutCommandResolve = "resolve";

        public const string IpcTimeoutCommandQuery = "query";

        public const string IpcTimeoutCommandRefresh = "refresh";

        public const string IpcTimeoutCommandOps = "ops";

        public const string IpcTimeoutCommandDaemonStart = "daemon.start";

        public const string IpcTimeoutCommandDaemonStop = "daemon.stop";

        public const string IpcTimeoutCommandDaemonCleanup = "daemon.cleanup";

        public const string IpcTimeoutCommandDaemonStatus = "daemon.status";

        public const string IpcTimeoutCommandDaemonList = "daemon.list";

        public const string IpcTimeoutCommandLogsDaemonRead = "logs.daemon.read";

        public const string IpcTimeoutCommandLogsUnityRead = "logs.unity.read";

        public const string IpcTimeoutCommandLogsUnityClear = "logs.unity.clear";
    }

    internal static class TestProfile
    {
        public const int SchemaVersion = 1;

        public const string ProjectPath = ".";

        public const string TestPlatformEditMode = "editmode";

        public const int TimeoutMilliseconds = 1800000;
    }

    public const string LocalDirectoryIgnoreEntry = "local/";
}
