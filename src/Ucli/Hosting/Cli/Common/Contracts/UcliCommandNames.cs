namespace MackySoft.Ucli.Hosting.Cli.Common.Contracts;

/// <summary> Defines CLI command-name constants and lookup helpers. </summary>
internal static class UcliCommandNames
{
    /// <summary> Gets the command name used when no subcommand can be identified. </summary>
    public const string Root = "root";

    /// <summary> Gets the command name for help. </summary>
    public const string Help = "help";

    /// <summary> Gets the command name for init. </summary>
    public const string Init = "init";

    /// <summary> Gets the command name for status. </summary>
    public const string Status = "status";

    /// <summary> Gets the command name for refresh. </summary>
    public const string Refresh = "refresh";

    /// <summary> Gets the command name for resolve. </summary>
    public const string Resolve = "resolve";

    /// <summary> Gets the top-level command name for query. </summary>
    public const string Query = "query";

    /// <summary> Gets the command name for <c>query assets find</c> result payloads. </summary>
    public const string QueryAssetsFind = "query.assets.find";

    /// <summary> Gets the command name for <c>query scene tree</c> result payloads. </summary>
    public const string QuerySceneTree = "query.scene.tree";

    /// <summary> Gets the command name for <c>query go describe</c> result payloads. </summary>
    public const string QueryGoDescribe = "query.go.describe";

    /// <summary> Gets the command name for <c>query comp schema</c> result payloads. </summary>
    public const string QueryCompSchema = "query.comp.schema";

    /// <summary> Gets the command name for <c>query asset schema</c> result payloads. </summary>
    public const string QueryAssetSchema = "query.asset.schema";

    /// <summary> Gets the command name for validate. </summary>
    public const string Validate = "validate";

    /// <summary> Gets the command name for plan. </summary>
    public const string Plan = "plan";

    /// <summary> Gets the command name for call. </summary>
    public const string Call = "call";

    /// <summary> Gets the top-level command name for daemon. </summary>
    public const string Daemon = "daemon";

    /// <summary> Gets the command name for <c>daemon start</c> result payloads. </summary>
    public const string DaemonStart = "daemon.start";

    /// <summary> Gets the command name for <c>daemon stop</c> result payloads. </summary>
    public const string DaemonStop = "daemon.stop";

    /// <summary> Gets the command name for <c>daemon cleanup</c> result payloads. </summary>
    public const string DaemonCleanup = "daemon.cleanup";

    /// <summary> Gets the command name for <c>daemon status</c> result payloads. </summary>
    public const string DaemonStatus = "daemon.status";

    /// <summary> Gets the command name for <c>daemon list</c> result payloads. </summary>
    public const string DaemonList = "daemon.list";

    /// <summary> Gets the top-level command name for logs. </summary>
    public const string Logs = "logs";

    /// <summary> Gets the top-level command name for ops. </summary>
    public const string Ops = "ops";

    /// <summary> Gets the top-level command name for skills. </summary>
    public const string Skills = "skills";

    /// <summary> Gets the command name for <c>logs daemon</c> result payloads. </summary>
    public const string LogsDaemon = "logs.daemon";

    /// <summary> Gets the command name for <c>logs unity</c> result payloads. </summary>
    public const string LogsUnity = "logs.unity";

    /// <summary> Gets the command name for <c>ops list</c> result payloads. </summary>
    public const string OpsList = "ops.list";

    /// <summary> Gets the command name for <c>ops describe</c> result payloads. </summary>
    public const string OpsDescribe = "ops.describe";

    /// <summary> Gets the command name for <c>skills list</c> result payloads. </summary>
    public const string SkillsList = "skills.list";

    /// <summary> Gets the command name for <c>skills export</c> result payloads. </summary>
    public const string SkillsExport = "skills.export";

    /// <summary> Gets the command name for <c>skills install</c> result payloads. </summary>
    public const string SkillsInstall = "skills.install";

    /// <summary> Gets the command name for <c>skills doctor</c> result payloads. </summary>
    public const string SkillsDoctor = "skills.doctor";

    /// <summary> Gets the top-level command name for test. </summary>
    public const string Test = "test";

    /// <summary> Gets the command name for <c>test profile init</c> result payloads. </summary>
    public const string TestProfileInit = "test.profile.init";

    /// <summary> Gets the command name for <c>test run</c> result payloads. </summary>
    public const string TestRun = "test.run";

    /// <summary> Gets the nested command name for profile. </summary>
    public const string Profile = "profile";

    /// <summary> Gets the nested command name for run. </summary>
    public const string RunSubcommand = "run";

    /// <summary> Gets the nested command name for init. </summary>
    public const string InitSubcommand = "init";

    /// <summary> Gets the nested command name for <c>ops list</c>. </summary>
    public const string ListSubcommand = "list";

    /// <summary> Gets the nested command name for <c>ops describe</c>. </summary>
    public const string DescribeSubcommand = "describe";

    /// <summary> Gets the nested command name for <c>skills export</c>. </summary>
    public const string ExportSubcommand = "export";

    /// <summary> Gets the nested command name for <c>skills install</c>. </summary>
    public const string InstallSubcommand = "install";

    /// <summary> Gets the nested command name for <c>skills doctor</c>. </summary>
    public const string DoctorSubcommand = "doctor";

    /// <summary> Gets the nested command name for daemon start. </summary>
    public const string StartSubcommand = "start";

    /// <summary> Gets the nested command name for daemon stop. </summary>
    public const string StopSubcommand = "stop";

    /// <summary> Gets the nested command name for daemon cleanup. </summary>
    public const string CleanupSubcommand = "cleanup";

    /// <summary> Gets the nested command name for logs unity target. </summary>
    public const string UnitySubcommand = "unity";

    /// <summary> Gets the nested command name for asset queries. </summary>
    public const string AssetSubcommand = "asset";

    /// <summary> Gets the nested command name for assets queries. </summary>
    public const string AssetsSubcommand = "assets";

    /// <summary> Gets the nested command name for component queries. </summary>
    public const string CompSubcommand = "comp";

    /// <summary> Gets the nested command name for GameObject queries. </summary>
    public const string GoSubcommand = "go";

    /// <summary> Gets the nested command name for scene queries. </summary>
    public const string SceneSubcommand = "scene";

    /// <summary> Gets the nested command name for find queries. </summary>
    public const string FindSubcommand = "find";

    /// <summary> Gets the nested command name for schema queries. </summary>
    public const string SchemaSubcommand = "schema";

    /// <summary> Gets the nested command name for scene tree queries. </summary>
    public const string TreeSubcommand = "tree";
}
