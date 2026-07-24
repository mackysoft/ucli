using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Startup;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartupObservationTests
{
    [Theory]
    [InlineData(InvalidEnumField.StartupStatus, "StartupStatus")]
    [InlineData(InvalidEnumField.StartupBlockingReason, "StartupBlockingReason")]
    [InlineData(InvalidEnumField.ProcessAction, "ProcessAction")]
    [InlineData(InvalidEnumField.RetryDisposition, "RetryDisposition")]
    [InlineData(InvalidEnumField.EditorMode, "EditorMode")]
    [InlineData(InvalidEnumField.OwnerKind, "OwnerKind")]
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
            InvalidEnumField.ProcessAction => CreateValidValues() with
            {
                ProcessAction = (DaemonStartupProcessAction)int.MaxValue,
            },
            InvalidEnumField.RetryDisposition => CreateValidValues() with
            {
                RetryDisposition = (DaemonStartupRetryDisposition)int.MaxValue,
            },
            InvalidEnumField.EditorMode => CreateValidValues() with
            {
                EditorMode = (DaemonEditorMode)int.MaxValue,
            },
            InvalidEnumField.OwnerKind => CreateValidValues() with
            {
                OwnerKind = (DaemonSessionOwnerKind)int.MaxValue,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateObservation(values));

        Assert.Equal(expectedParameterName, exception.ParamName);
    }

    [Theory]
    [InlineData(DaemonStartupStatus.Launching)]
    [InlineData(DaemonStartupStatus.WaitingForEndpoint)]
    [InlineData(DaemonStartupStatus.Completed)]
    [Trait("Size", "Small")]
    public void Constructor_WithNonFailureStatus_ThrowsArgumentOutOfRangeException (DaemonStartupStatus startupStatus)
    {
        var values = CreateValidValues() with
        {
            StartupStatus = startupStatus,
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateObservation(values));

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

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateObservation(values));

        Assert.Equal("ProcessId", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithNonUtcStartedAt_ThrowsArgumentException ()
    {
        var values = CreateValidValues() with
        {
            StartedAtUtc = new DateTimeOffset(2026, 7, 14, 9, 0, 0, TimeSpan.FromHours(9)),
        };

        var exception = Assert.Throws<ArgumentException>(() => CreateObservation(values));

        Assert.Equal("StartedAtUtc", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_WithNegativeElapsedMilliseconds_ThrowsArgumentOutOfRangeException ()
    {
        var values = CreateValidValues() with
        {
            ElapsedMilliseconds = -1,
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateObservation(values));

        Assert.Equal("ElapsedMilliseconds", exception.ParamName);
    }

    [Fact]
    [Trait("Size", "Small")]
    public void Constructor_ParametersHaveNoDefaultValues ()
    {
        var constructor = Assert.Single(typeof(DaemonStartupObservation).GetConstructors());

        Assert.All(constructor.GetParameters(), static parameter => Assert.False(parameter.HasDefaultValue));
    }

    private static DaemonStartupObservation CreateObservation (StartupObservationValues values)
    {
        return new DaemonStartupObservation(
            values.StartupStatus,
            values.StartupBlockingReason,
            values.LaunchAttemptId,
            values.ProcessAction,
            values.RetryDisposition,
            values.EditorMode,
            values.OwnerKind,
            values.CanShutdownProcess,
            values.ProcessId,
            values.StartedAtUtc,
            values.ElapsedMilliseconds,
            values.ArtifactPath);
    }

    private static StartupObservationValues CreateValidValues ()
    {
        return new StartupObservationValues(
            DaemonStartupStatus.Failed,
            DaemonStartupBlockingReason.Unknown,
            Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
            DaemonStartupProcessAction.None,
            DaemonStartupRetryDisposition.Unknown,
            DaemonEditorMode.Batchmode,
            DaemonSessionOwnerKind.Cli,
            true,
            1234,
            new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero),
            1000,
            AbsolutePath.Parse(Path.Combine(
                ProjectPathTestValues.RepositoryRoot,
                ".ucli",
                "launch-attempts",
                "startup-diagnosis.json")));
    }

    public enum InvalidEnumField
    {
        StartupStatus,
        StartupBlockingReason,
        ProcessAction,
        RetryDisposition,
        EditorMode,
        OwnerKind,
    }

    private sealed record StartupObservationValues (
        DaemonStartupStatus StartupStatus,
        DaemonStartupBlockingReason StartupBlockingReason,
        Guid? LaunchAttemptId,
        DaemonStartupProcessAction ProcessAction,
        DaemonStartupRetryDisposition RetryDisposition,
        DaemonEditorMode? EditorMode,
        DaemonSessionOwnerKind? OwnerKind,
        bool? CanShutdownProcess,
        int? ProcessId,
        DateTimeOffset? StartedAtUtc,
        int? ElapsedMilliseconds,
        AbsolutePath? ArtifactPath);
}
