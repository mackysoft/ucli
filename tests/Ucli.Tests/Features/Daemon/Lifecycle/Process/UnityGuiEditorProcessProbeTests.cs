using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
namespace MackySoft.Ucli.Tests.Daemon;

public sealed class UnityGuiEditorProcessProbeTests
{
    private const string DefaultInspectionCommandLine = "__ucli_default_command_line__";

    private const string DefaultInspectionExecutablePath = "__ucli_default_executable_path__";

    private static readonly DateTimeOffset MarkerUpdatedAtUtc = new(2026, 03, 12, 12, 0, 0, TimeSpan.Zero);

    private static readonly string UnityApplicationPath = OperatingSystem.IsWindows()
        ? @"C:\Program Files\Unity\Hub\Editor\6000.1.4f1\Editor\Unity.exe"
        : "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app";

    private static readonly string UnityApplicationContentsPath = OperatingSystem.IsWindows()
        ? @"C:\Program Files\Unity\Hub\Editor\6000.1.4f1\Editor"
        : "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents";

    private static readonly string UnityExecutablePath = OperatingSystem.IsWindows()
        ? UnityApplicationPath
        : "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity";

    private static readonly string UnityExecutableSiblingPath = OperatingSystem.IsWindows()
        ? @"C:\Program Files\Unity\Hub\Editor\6000.1.4f1\Editor\Unity.exeSibling"
        : "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.appSibling/Contents/MacOS/Unity";

    private static readonly string NonUnityExecutablePath = OperatingSystem.IsWindows()
        ? @"C:\Tools\NotEditor\not-editor.exe"
        : "/usr/bin/not-editor";

    private static readonly string UnityCommandLine = OperatingSystem.IsWindows()
        ? $@"""{UnityExecutablePath}"" -projectPath C:\project"
        : "/Applications/Unity -projectPath /project";

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
            commandLine: NonUnityExecutablePath,
            executablePath: NonUnityExecutablePath));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.NotUnityEditor, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenExecutablePathMatchesMarkerApplicationPath_ReturnsMatching ()
    {
        var result = await ProbeAsync(CreateInspection(
            processName: "Editor",
            commandLine: UnityCommandLine,
            executablePath: UnityExecutablePath));

        Assert.True(result.IsMatchingGuiEditor);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenExecutablePathOnlySharesMarkerPrefix_ReturnsNotUnityEditor ()
    {
        var result = await ProbeAsync(CreateInspection(
            executablePath: UnityExecutableSiblingPath));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.NotUnityEditor, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenMarkerApplicationPathIsRoot_ReturnsNotUnityEditor ()
    {
        var result = await ProbeAsync(
            CreateInspection(executablePath: "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity"),
            CreateMarker("/", null));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.NotUnityEditor, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenMarkerApplicationPathIsBroadParentDirectory_ReturnsNotUnityEditor ()
    {
        var result = await ProbeAsync(
            CreateInspection(executablePath: "/Applications/Unity/Hub/Editor/6000.1.4f1/Unity.app/Contents/MacOS/Unity"),
            CreateMarker("/Applications", null));

        Assert.Equal(UnityGuiEditorProcessProbeStatus.NotUnityEditor, result.Status);
    }

    [Fact]
    [Trait("Size", "Small")]
    public async Task Probe_WhenMarkerHasOnlyProcessIdAndExecutableLooksLikeUnityEditor_ReturnsMatching ()
    {
        var result = await ProbeAsync(
            CreateInspection(
                processName: "Unity",
                commandLine: UnityCommandLine,
                executablePath: UnityExecutablePath),
            CreateMarker(null, null));

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

    private static UnityEditorInstanceMarker CreateMarker ()
    {
        return CreateMarker(UnityApplicationPath, UnityApplicationContentsPath);
    }

    private static UnityEditorInstanceMarker CreateMarker (
        string? appPath,
        string? appContentsPath)
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
        string? commandLine = DefaultInspectionCommandLine,
        string? executablePath = DefaultInspectionExecutablePath,
        bool? isOwnedByCurrentUser = true)
    {
        return new UnityGuiEditorProcessInspection(
            Exists: true,
            HasExited: false,
            StartTimeUtc: startTimeAvailable
                ? startTimeUtc ?? MarkerUpdatedAtUtc.AddSeconds(-1)
                : null,
            ProcessName: processName,
            CommandLine: string.Equals(commandLine, DefaultInspectionCommandLine, StringComparison.Ordinal)
                ? UnityCommandLine
                : commandLine,
            ExecutablePath: string.Equals(executablePath, DefaultInspectionExecutablePath, StringComparison.Ordinal)
                ? UnityExecutablePath
                : executablePath,
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
