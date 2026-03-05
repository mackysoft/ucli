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

        public const int IpcDefaultTimeoutMilliseconds = 1800000;

        public const string IpcTimeoutCommandTest = "test";

        public const string IpcTimeoutCommandStatus = "status";

        public const string IpcTimeoutCommandValidate = "validate";

        public const string IpcTimeoutCommandPlan = "plan";

        public const string IpcTimeoutCommandCall = "call";

        public const string IpcTimeoutCommandResolve = "resolve";

        public const string IpcTimeoutCommandQuery = "query";

        public const string IpcTimeoutCommandRefresh = "refresh";

        public const string IpcTimeoutCommandOps = "ops";

        public const string IpcTimeoutCommandDaemon = "daemon";
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