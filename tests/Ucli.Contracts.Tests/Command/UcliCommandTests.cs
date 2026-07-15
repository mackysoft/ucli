using MackySoft.Ucli.Contracts.Ipc;

namespace MackySoft.Ucli.Contracts.Tests.Command;

public sealed class UcliCommandTests
{
    private static readonly string[] ValidCommandNames =
    [
        "status",
        "daemon.cleanup",
        "daemon.status",
        "daemon.list",
        "build.run",
        "test.run",
        "logs.daemon.read",
        "logs.unity.read",
        "logs.unity.clear",
        "screenshot.game",
        "screenshot.scene",
    ];

    private static readonly string?[] InvalidCommandNames =
    [
        null,
        "",
        " ",
        "daemon status",
        "daemon\tstatus",
        "daemon\rstatus",
        "daemon\nstatus",
        "\u00a0",
        ".daemon",
        "daemon.",
        "daemon..status",
    ];

    private static readonly CommandLiteralCase[] CommandLiteralCases =
    [
        new(UcliCommandIds.Play, "play"),
        new(UcliCommandIds.PlayStatus, "play.status"),
        new(UcliCommandIds.PlayEnter, "play.enter"),
        new(UcliCommandIds.PlayExit, "play.exit"),
        new(UcliCommandIds.Build, "build"),
        new(UcliCommandIds.BuildRun, "build.run"),
        new(UcliCommandIds.Validate, "validate"),
        new(UcliCommandIds.Plan, "plan"),
        new(UcliCommandIds.Call, "call"),
        new(UcliCommandIds.Resolve, "resolve"),
        new(UcliCommandIds.Query, "query"),
        new(UcliCommandIds.Refresh, "refresh"),
        new(UcliCommandIds.Ready, "ready"),
        new(UcliCommandIds.Logs, "logs"),
        new(UcliCommandIds.LogsDaemonRead, "logs.daemon.read"),
        new(UcliCommandIds.LogsUnityRead, "logs.unity.read"),
        new(UcliCommandIds.LogsUnityClear, "logs.unity.clear"),
        new(UcliCommandIds.Screenshot, "screenshot"),
        new(UcliCommandIds.ScreenshotGame, "screenshot.game"),
        new(UcliCommandIds.ScreenshotScene, "screenshot.scene"),
        new(UcliCommandIds.Codes, "codes"),
        new(UcliCommandIds.CodesList, "codes.list"),
        new(UcliCommandIds.CodesDescribe, "codes.describe"),
        new(UcliCommandIds.Daemon, "daemon"),
        new(UcliCommandIds.DaemonStart, "daemon.start"),
        new(UcliCommandIds.DaemonStop, "daemon.stop"),
        new(UcliCommandIds.DaemonCleanup, "daemon.cleanup"),
        new(UcliCommandIds.DaemonStatus, "daemon.status"),
        new(UcliCommandIds.DaemonList, "daemon.list"),
    ];

    private static readonly ExecuteCommandClassificationCase[] ExecuteCommandClassificationCases =
    [
        new(UcliCommandIds.Validate.Name, IsKnown: true, IsOperationPipelineCommand: false),
        new(UcliCommandIds.Plan.Name, IsKnown: true, IsOperationPipelineCommand: true),
        new(UcliCommandIds.Call.Name, IsKnown: true, IsOperationPipelineCommand: true),
        new(UcliCommandIds.Resolve.Name, IsKnown: true, IsOperationPipelineCommand: true),
        new(UcliCommandIds.Query.Name, IsKnown: true, IsOperationPipelineCommand: true),
        new(UcliCommandIds.Refresh.Name, IsKnown: false, IsOperationPipelineCommand: false),
        new(UcliCommandIds.Ready.Name, IsKnown: false, IsOperationPipelineCommand: false),
        new("unknown", IsKnown: false, IsOperationPipelineCommand: false),
    ];

    [Fact]
    [Trait("Size", "Small")]
    public void ValidCommandNames_CanCreateCommandIdentifier ()
    {
        foreach (string name in ValidCommandNames)
        {
            Assert.True(UcliCommand.IsValidName(name), $"{name} must be accepted as a command name.");
            var command = new UcliCommand(name);
            Assert.True(UcliCommand.TryCreate(name, out var parsedCommand));
            Assert.NotNull(parsedCommand);
            Assert.Equal(name, command.Name);
            Assert.Equal(name, command.ToString());
            Assert.Equal(command, parsedCommand);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidName_WithInvalidName_ReturnsFalse ()
    {
        foreach (string? name in InvalidCommandNames)
        {
            Assert.False(UcliCommand.IsValidName(name));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IsValidName_WithValidName_DoesNotAllocate ()
    {
        const string Name = "daemon.status";
        for (var index = 0; index < 16; index++)
        {
            _ = UcliCommand.IsValidName(Name);
        }

        var allocatedBytesBefore = GC.GetAllocatedBytesForCurrentThread();
        var isValid = true;
        for (var index = 0; index < 1_000; index++)
        {
            isValid &= UcliCommand.IsValidName(Name);
        }

        var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBytesBefore;
        Assert.True(isValid);
        Assert.Equal(0, allocatedBytes);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void TryCreate_WithInvalidName_ReturnsNull ()
    {
        foreach (string? name in InvalidCommandNames)
        {
            Assert.False(UcliCommand.TryCreate(name, out var command));
            Assert.Null(command);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void ConstructionModel_UsesPrivateValidatedValueConstructor ()
    {
    }

    [Fact]
    [Trait("Size", "Small")]
    public void UcliCommandIds_ExposeExpectedLiterals ()
    {
        foreach (var testCase in CommandLiteralCases)
        {
            Assert.Equal(testCase.ExpectedLiteral, testCase.Command.Name);
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void IpcExecuteCommandNames_ClassifiesKnownAndOperationPipelineCommands ()
    {
        foreach (var testCase in ExecuteCommandClassificationCases)
        {
            Assert.Equal(testCase.IsKnown, IpcExecuteCommandNames.IsKnown(testCase.CommandName));
            Assert.Equal(
                testCase.IsOperationPipelineCommand,
                IpcExecuteCommandNames.IsOperationPipelineCommand(testCase.CommandName));
        }
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PublicCommandCatalog_IncludesPlayModeCommandFamily ()
    {
        Assert.Contains(UcliCommandIds.Play, UcliPublicCommandCatalog.KnownCommands);
        Assert.Contains(UcliCommandIds.PlayStatus, UcliPublicCommandCatalog.KnownCommands);
        Assert.Contains(UcliCommandIds.PlayEnter, UcliPublicCommandCatalog.KnownCommands);
        Assert.Contains(UcliCommandIds.PlayExit, UcliPublicCommandCatalog.KnownCommands);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PublicCommandCatalog_IncludesBuildCommandFamily ()
    {
        Assert.Contains(UcliCommandIds.Build, UcliPublicCommandCatalog.KnownCommands);
        Assert.Contains(UcliCommandIds.BuildRun, UcliPublicCommandCatalog.KnownCommands);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void PublicCommandCatalog_IncludesScreenshotCommandFamily ()
    {
        Assert.Contains(UcliCommandIds.Screenshot, UcliPublicCommandCatalog.KnownCommands);
        Assert.Contains(UcliCommandIds.ScreenshotGame, UcliPublicCommandCatalog.KnownCommands);
        Assert.Contains(UcliCommandIds.ScreenshotScene, UcliPublicCommandCatalog.KnownCommands);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithInvalidName_ThrowsArgumentException ()
    {
        foreach (string? name in InvalidCommandNames)
        {
            Assert.Throws<ArgumentException>(() =>
            {
                _ = new UcliCommand(name!);
            });
        }
    }

    private sealed record CommandLiteralCase (
        UcliCommand Command,
        string ExpectedLiteral);

    private sealed record ExecuteCommandClassificationCase (
        string CommandName,
        bool IsKnown,
        bool IsOperationPipelineCommand);
}
