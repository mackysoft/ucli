using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
namespace MackySoft.Ucli.Tests.Daemon;

public sealed class UnityGuiEditorProcessProbeTests
{
    private static readonly DateTimeOffset MarkerUpdatedAtUtc = new(2026, 03, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenProcessStartsAfterMarker_ReturnsStaleMarker ()
    {
        var result = await ProbeAsync(CreateInspection(startTimeUtc: MarkerUpdatedAtUtc.AddSeconds(1)));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.StaleMarker, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenCommandLineContainsBatchmode_ReturnsBatchmode ()
    {
        var result = await ProbeAsync(CreateInspection(commandLine: "/Applications/Unity -batchmode -projectPath /project"));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.Batchmode, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenProcessBelongsToDifferentUser_ReturnsDifferentUser ()
    {
        var result = await ProbeAsync(CreateInspection(isOwnedByCurrentUser: false));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.DifferentUser, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenProcessOwnerCannotBeVerified_ReturnsUncertain ()
    {
        var result = await ProbeAsync(CreateInspection(isOwnedByCurrentUser: null));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.Uncertain, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenCommandLineCannotBeRead_ReturnsUncertain ()
    {
        var result = await ProbeAsync(CreateInspection(commandLine: null));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.Uncertain, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenProcessDoesNotLookLikeUnityEditor_ReturnsNotUnityEditor ()
    {
        var result = await ProbeAsync(CreateInspection(
            processName: "NotEditor",
            commandLine: "/usr/bin/not-editor",
            executablePath: "/usr/bin/not-editor"));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.NotUnityEditor, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenExecutablePathMatchesMarkerApplicationPath_ReturnsMatching ()
    {
        var result = await ProbeAsync(CreateInspection(
            processName: "Editor",
            commandLine: "/Applications/Editor -projectPath /project",
            executablePath: "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity"));

        Assert.True(result.IsMatchingGuiEditor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenExecutablePathOnlySharesMarkerPrefix_ReturnsNotUnityEditor ()
    {
        var result = await ProbeAsync(CreateInspection(
            executablePath: "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.appSibling/Contents/MacOS/Unity"));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.NotUnityEditor, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenMarkerApplicationPathIsRoot_ReturnsNotUnityEditor ()
    {
        var result = await ProbeAsync(
            CreateInspection(executablePath: "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity"),
            CreateMarker(appPath: "/", appContentsPath: null));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.NotUnityEditor, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenMarkerApplicationPathIsBroadParentDirectory_ReturnsNotUnityEditor ()
    {
        var result = await ProbeAsync(
            CreateInspection(executablePath: "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity"),
            CreateMarker(appPath: "/Applications", appContentsPath: null));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.NotUnityEditor, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenMarkerHasOnlyProcessIdAndExecutableLooksLikeUnityEditor_ReturnsMatching ()
    {
        var result = await ProbeAsync(
            CreateInspection(
                processName: "Unity",
                commandLine: "/Applications/Unity -projectPath /project",
                executablePath: "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity"),
            CreateMarker(appPath: null, appContentsPath: null));

        Assert.True(result.IsMatchingGuiEditor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenStartTimeCannotBeRead_ReturnsUncertain ()
    {
        var result = await ProbeAsync(CreateInspection(startTimeAvailable: false));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.Uncertain, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenProcessIsCurrentUserGuiUnityEditor_ReturnsMatching ()
    {
        var result = await ProbeAsync(CreateInspection());

        Assert.True(result.IsMatchingGuiEditor);
        Assert.Equal(UnityGuiEditorProcessProbeStatus.MatchingGuiEditor, result.Status);
    }

    private static ValueTask<UnityGuiEditorProcessProbeResult> ProbeAsync (
        UnityGuiEditorProcessInspection inspection,
        UnityEditorInstanceMarker? marker = null)
    {
        var probe = new UnityGuiEditorProcessProbe(new StubUnityGuiEditorProcessInspector(inspection));
        return probe.ProbeAsync(marker ?? CreateMarker(), CancellationToken.None);
    }

    private static UnityEditorInstanceMarker CreateMarker (
        string? appPath = "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app",
        string? appContentsPath = "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents")
    {
        return new UnityEditorInstanceMarker(
            MarkerPath: "/project/Library/EditorInstance.json",
            ProcessId: 1234,
            UpdatedAtUtc: MarkerUpdatedAtUtc,
            AppPath: appPath,
            AppContentsPath: appContentsPath);
    }

    private static UnityGuiEditorProcessInspection CreateInspection (
        DateTimeOffset? startTimeUtc = null,
        bool startTimeAvailable = true,
        string? processName = "Unity",
        string? commandLine = "/Applications/Unity -projectPath /project",
        string? executablePath = "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity",
        bool? isOwnedByCurrentUser = true)
    {
        return new UnityGuiEditorProcessInspection(
            Exists: true,
            HasExited: false,
            StartTimeUtc: startTimeAvailable
                ? startTimeUtc ?? MarkerUpdatedAtUtc.AddSeconds(-1)
                : null,
            ProcessName: processName,
            CommandLine: commandLine,
            ExecutablePath: executablePath,
            IsOwnedByCurrentUser: isOwnedByCurrentUser);
    }

    private sealed class StubUnityGuiEditorProcessInspector : IUnityGuiEditorProcessInspector
    {
        private readonly UnityGuiEditorProcessInspection inspection;

        public StubUnityGuiEditorProcessInspector (UnityGuiEditorProcessInspection inspection)
        {
            this.inspection = inspection;
        }

        public UnityGuiEditorProcessInspection Inspect (int processId)
        {
            return inspection;
        }
    }
}
