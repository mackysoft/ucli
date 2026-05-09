namespace MackySoft.Ucli.Contracts;

/// <summary> Defines canonical command identifiers used across CLI and IPC domains. </summary>
public static class UcliCommandIds
{
    /// <summary> Gets command identifier for <c>init</c>. </summary>
    public static UcliCommand Init { get; } = new("init");

    /// <summary> Gets command identifier for <c>status</c>. </summary>
    public static UcliCommand Status { get; } = new("status");

    /// <summary> Gets command identifier for <c>daemon</c>. </summary>
    public static UcliCommand Daemon { get; } = new("daemon");

    /// <summary> Gets command identifier for <c>daemon.start</c>. </summary>
    public static UcliCommand DaemonStart { get; } = new("daemon.start");

    /// <summary> Gets command identifier for <c>daemon.stop</c>. </summary>
    public static UcliCommand DaemonStop { get; } = new("daemon.stop");

    /// <summary> Gets command identifier for <c>daemon.cleanup</c>. </summary>
    public static UcliCommand DaemonCleanup { get; } = new("daemon.cleanup");

    /// <summary> Gets command identifier for <c>daemon.status</c>. </summary>
    public static UcliCommand DaemonStatus { get; } = new("daemon.status");

    /// <summary> Gets command identifier for <c>daemon.list</c>. </summary>
    public static UcliCommand DaemonList { get; } = new("daemon.list");

    /// <summary> Gets command identifier for <c>logs</c>. </summary>
    public static UcliCommand Logs { get; } = new("logs");

    /// <summary> Gets command identifier for <c>logs.daemon</c>. </summary>
    public static UcliCommand LogsDaemon { get; } = new("logs.daemon");

    /// <summary> Gets command identifier for <c>logs.daemon.read</c>. </summary>
    public static UcliCommand LogsDaemonRead { get; } = new("logs.daemon.read");

    /// <summary> Gets command identifier for <c>logs.unity</c>. </summary>
    public static UcliCommand LogsUnity { get; } = new("logs.unity");

    /// <summary> Gets command identifier for <c>logs.unity.read</c>. </summary>
    public static UcliCommand LogsUnityRead { get; } = new("logs.unity.read");

    /// <summary> Gets command identifier for <c>logs.unity.clear</c>. </summary>
    public static UcliCommand LogsUnityClear { get; } = new("logs.unity.clear");

    /// <summary> Gets command identifier for <c>errors</c>. </summary>
    public static UcliCommand Errors { get; } = new("errors");

    /// <summary> Gets command identifier for <c>errors.list</c>. </summary>
    public static UcliCommand ErrorsList { get; } = new("errors.list");

    /// <summary> Gets command identifier for <c>errors.describe</c>. </summary>
    public static UcliCommand ErrorsDescribe { get; } = new("errors.describe");

    /// <summary> Gets command identifier for <c>test</c>. </summary>
    public static UcliCommand Test { get; } = new("test");

    /// <summary> Gets command identifier for <c>test.run</c>. </summary>
    public static UcliCommand TestRun { get; } = new("test.run");

    /// <summary> Gets command identifier for <c>test.profile</c>. </summary>
    public static UcliCommand TestProfile { get; } = new("test.profile");

    /// <summary> Gets command identifier for <c>test.profile.init</c>. </summary>
    public static UcliCommand TestProfileInit { get; } = new("test.profile.init");

    /// <summary> Gets command identifier for <c>validate</c>. </summary>
    public static UcliCommand Validate { get; } = new("validate");

    /// <summary> Gets command identifier for <c>plan</c>. </summary>
    public static UcliCommand Plan { get; } = new("plan");

    /// <summary> Gets command identifier for <c>call</c>. </summary>
    public static UcliCommand Call { get; } = new("call");

    /// <summary> Gets command identifier for <c>resolve</c>. </summary>
    public static UcliCommand Resolve { get; } = new("resolve");

    /// <summary> Gets command identifier for <c>query</c>. </summary>
    public static UcliCommand Query { get; } = new("query");

    /// <summary> Gets command identifier for <c>query.assets</c>. </summary>
    public static UcliCommand QueryAssets { get; } = new("query.assets");

    /// <summary> Gets command identifier for <c>query.assets.find</c>. </summary>
    public static UcliCommand QueryAssetsFind { get; } = new("query.assets.find");

    /// <summary> Gets command identifier for <c>query.scene</c>. </summary>
    public static UcliCommand QueryScene { get; } = new("query.scene");

    /// <summary> Gets command identifier for <c>query.scene.tree</c>. </summary>
    public static UcliCommand QuerySceneTree { get; } = new("query.scene.tree");

    /// <summary> Gets command identifier for <c>query.go</c>. </summary>
    public static UcliCommand QueryGo { get; } = new("query.go");

    /// <summary> Gets command identifier for <c>query.go.describe</c>. </summary>
    public static UcliCommand QueryGoDescribe { get; } = new("query.go.describe");

    /// <summary> Gets command identifier for <c>query.comp</c>. </summary>
    public static UcliCommand QueryComp { get; } = new("query.comp");

    /// <summary> Gets command identifier for <c>query.comp.schema</c>. </summary>
    public static UcliCommand QueryCompSchema { get; } = new("query.comp.schema");

    /// <summary> Gets command identifier for <c>query.asset</c>. </summary>
    public static UcliCommand QueryAsset { get; } = new("query.asset");

    /// <summary> Gets command identifier for <c>query.asset.schema</c>. </summary>
    public static UcliCommand QueryAssetSchema { get; } = new("query.asset.schema");

    /// <summary> Gets command identifier for <c>refresh</c>. </summary>
    public static UcliCommand Refresh { get; } = new("refresh");

    /// <summary> Gets command identifier for <c>ops</c>. </summary>
    public static UcliCommand Ops { get; } = new("ops");

    /// <summary> Gets command identifier for <c>ops.list</c>. </summary>
    public static UcliCommand OpsList { get; } = new("ops.list");

    /// <summary> Gets command identifier for <c>ops.describe</c>. </summary>
    public static UcliCommand OpsDescribe { get; } = new("ops.describe");

    /// <summary> Gets command identifier for <c>skills</c>. </summary>
    public static UcliCommand Skills { get; } = new("skills");

    /// <summary> Gets command identifier for <c>skills.list</c>. </summary>
    public static UcliCommand SkillsList { get; } = new("skills.list");

    /// <summary> Gets command identifier for <c>skills.export</c>. </summary>
    public static UcliCommand SkillsExport { get; } = new("skills.export");

    /// <summary> Gets command identifier for <c>skills.install</c>. </summary>
    public static UcliCommand SkillsInstall { get; } = new("skills.install");

    /// <summary> Gets command identifier for <c>skills.update</c>. </summary>
    public static UcliCommand SkillsUpdate { get; } = new("skills.update");

    /// <summary> Gets command identifier for <c>skills.uninstall</c>. </summary>
    public static UcliCommand SkillsUninstall { get; } = new("skills.uninstall");

    /// <summary> Gets command identifier for <c>skills.doctor</c>. </summary>
    public static UcliCommand SkillsDoctor { get; } = new("skills.doctor");
}
