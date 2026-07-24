using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.LaunchAttempts;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonLaunchAttemptTests
{
    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithEmptyLaunchAttemptId_ThrowsArgumentException ()
    {
        var values = CreateValidValues() with
        {
            LaunchAttemptId = Guid.Empty,
        };

        var exception = Assert.Throws<ArgumentException>(() => CreateLaunchAttempt(values));

        Assert.Equal("LaunchAttemptId", exception.ParamName);
    }

    [Theory]
    [InlineData(InvalidEnumField.StartupStatus, "StartupStatus")]
    [InlineData(InvalidEnumField.StartupBlockingReason, "StartupBlockingReason")]
    [InlineData(InvalidEnumField.RetryDisposition, "RetryDisposition")]
    [InlineData(InvalidEnumField.ProcessAction, "ProcessAction")]
    [InlineData(InvalidEnumField.EditorMode, "EditorMode")]
    [Trait("Size", "Small")]
    public void Constructor_WithUndefinedEnum_ThrowsArgumentOutOfRangeException (
        InvalidEnumField field,
        string expectedParameterName)
    {
        var values = field switch
        {
            InvalidEnumField.StartupStatus => CreateValidValues() with
            {
                StartupStatus = (DaemonStartupStatus)int.MaxValue,
            },
            InvalidEnumField.StartupBlockingReason => CreateValidValues() with
            {
                StartupBlockingReason = (DaemonStartupBlockingReason)int.MaxValue,
            },
            InvalidEnumField.RetryDisposition => CreateValidValues() with
            {
                RetryDisposition = (DaemonStartupRetryDisposition)int.MaxValue,
            },
            InvalidEnumField.ProcessAction => CreateValidValues() with
            {
                ProcessAction = (DaemonStartupProcessAction)int.MaxValue,
            },
            InvalidEnumField.EditorMode => CreateValidValues() with
            {
                EditorMode = (DaemonEditorMode)int.MaxValue,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateLaunchAttempt(values));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Theory]
    [InlineData(DaemonStartupStatus.Launching)]
    [InlineData(DaemonStartupStatus.WaitingForEndpoint)]
    [Trait("Size", "Small")]
    public void Constructor_WithNonTerminalStartupStatus_ThrowsArgumentOutOfRangeException (
        DaemonStartupStatus startupStatus)
    {
        var values = CreateValidValues() with
        {
            StartupStatus = startupStatus,
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateLaunchAttempt(values));

        Assert.Equal("StartupStatus", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [Trait("Size", "Small")]
    public void Constructor_WithNonPositiveProcessId_ThrowsArgumentOutOfRangeException (int processId)
    {
        var values = CreateValidValues() with
        {
            ProcessId = processId,
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateLaunchAttempt(values));

        Assert.Equal("ProcessId", exception.ParamName);
    }

    [Theory]
    [InlineData(TimestampField.StartedAtUtc, "StartedAtUtc")]
    [InlineData(TimestampField.UpdatedAtUtc, "UpdatedAtUtc")]
    [InlineData(TimestampField.ProcessStartedAtUtc, "ProcessStartedAtUtc")]
    [Trait("Size", "Small")]
    public void Constructor_WithNonUtcTimestamp_ThrowsArgumentException (
        TimestampField field,
        string expectedParameterName)
    {
        var nonUtcTimestamp = new DateTimeOffset(2026, 7, 14, 9, 0, 0, TimeSpan.FromHours(9));
        var values = field switch
        {
            TimestampField.StartedAtUtc => CreateValidValues() with
            {
                StartedAtUtc = nonUtcTimestamp,
            },
            TimestampField.UpdatedAtUtc => CreateValidValues() with
            {
                UpdatedAtUtc = nonUtcTimestamp,
            },
            TimestampField.ProcessStartedAtUtc => CreateValidValues() with
            {
                ProcessStartedAtUtc = nonUtcTimestamp,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };

        var exception = Assert.Throws<ArgumentException>(() => CreateLaunchAttempt(values));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenUpdatedAtPrecedesStartedAt_ThrowsArgumentException ()
    {
        var values = CreateValidValues() with
        {
            UpdatedAtUtc = new DateTimeOffset(2026, 7, 13, 23, 59, 59, TimeSpan.Zero),
        };

        var exception = Assert.Throws<ArgumentException>(() => CreateLaunchAttempt(values));

        Assert.Equal("UpdatedAtUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WhenProcessStartedAtFollowsUpdatedAt_ThrowsArgumentException ()
    {
        var values = CreateValidValues() with
        {
            ProcessStartedAtUtc = new DateTimeOffset(2026, 7, 14, 0, 0, 2, TimeSpan.Zero),
        };

        var exception = Assert.Throws<ArgumentException>(() => CreateLaunchAttempt(values));

        Assert.Equal("ProcessStartedAtUtc", exception.ParamName);
    }

    private static DaemonLaunchAttempt CreateLaunchAttempt (LaunchAttemptValues values)
    {
        return new DaemonLaunchAttempt(
            values.LaunchAttemptId,
            values.StartedAtUtc,
            values.UpdatedAtUtc,
            values.StartupStatus,
            values.StartupBlockingReason,
            values.RetryDisposition,
            values.ProcessAction,
            values.EditorMode,
            values.ProcessId,
            values.ProcessStartedAtUtc,
            values.UnityLogPath,
            values.ArtifactPath,
            values.Diagnosis);
    }

    private static LaunchAttemptValues CreateValidValues ()
    {
        return new LaunchAttemptValues(
            Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
            new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 7, 14, 0, 0, 1, TimeSpan.Zero),
            DaemonStartupStatus.Failed,
            DaemonStartupBlockingReason.Unknown,
            DaemonStartupRetryDisposition.Unknown,
            DaemonStartupProcessAction.None,
            DaemonEditorMode.Batchmode,
            1234,
            new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero),
            null,
            AbsolutePath.Parse(Path.Combine(
                ProjectPathTestValues.RepositoryRoot,
                ".ucli",
                "launch-attempts",
                "startup-diagnosis.json")),
            DaemonDiagnosisTestFactory.Create());
    }

    public enum InvalidEnumField
    {
        StartupStatus,
        StartupBlockingReason,
        RetryDisposition,
        ProcessAction,
        EditorMode,
    }

    public enum TimestampField
    {
        StartedAtUtc,
        UpdatedAtUtc,
        ProcessStartedAtUtc,
    }

    private sealed record LaunchAttemptValues (
        Guid LaunchAttemptId,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        DaemonStartupStatus StartupStatus,
        DaemonStartupBlockingReason StartupBlockingReason,
        DaemonStartupRetryDisposition RetryDisposition,
        DaemonStartupProcessAction ProcessAction,
        DaemonEditorMode? EditorMode,
        int? ProcessId,
        DateTimeOffset? ProcessStartedAtUtc,
        AbsolutePath? UnityLogPath,
        AbsolutePath ArtifactPath,
        MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis.DaemonDiagnosis Diagnosis);
}
