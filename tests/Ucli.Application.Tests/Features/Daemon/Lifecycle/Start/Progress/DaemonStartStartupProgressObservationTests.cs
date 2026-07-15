using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start.Progress;
using MackySoft.Ucli.Contracts.Storage;

namespace MackySoft.Ucli.Application.Tests.Daemon;

public sealed class DaemonStartStartupProgressObservationTests
{
    [Theory]
    [InlineData(InvalidEnumField.EditorMode, "EditorMode")]
    [InlineData(InvalidEnumField.OwnerKind, "OwnerKind")]
    [InlineData(InvalidEnumField.StartupStatus, "StartupStatus")]
    [InlineData(InvalidEnumField.StartupBlockingReason, "StartupBlockingReason")]
    [InlineData(InvalidEnumField.StartupPhase, "StartupPhase")]
    [InlineData(InvalidEnumField.RetryDisposition, "RetryDisposition")]
    [Trait("Size", "Small")]
    public void Constructor_WithUndefinedEnum_ThrowsArgumentOutOfRangeException (
        InvalidEnumField field,
        string expectedParameterName)
    {
        var values = field switch
        {
            InvalidEnumField.EditorMode => CreateValidValues() with
            {
                EditorMode = (DaemonEditorMode)int.MaxValue,
            },
            InvalidEnumField.OwnerKind => CreateValidValues() with
            {
                OwnerKind = (DaemonSessionOwnerKind)int.MaxValue,
            },
            InvalidEnumField.StartupStatus => CreateValidValues() with
            {
                StartupStatus = (DaemonStartupStatus)int.MaxValue,
            },
            InvalidEnumField.StartupBlockingReason => CreateValidValues() with
            {
                StartupBlockingReason = (DaemonStartupBlockingReason)int.MaxValue,
            },
            InvalidEnumField.StartupPhase => CreateValidValues() with
            {
                StartupPhase = (DaemonDiagnosisStartupPhase)int.MaxValue,
            },
            InvalidEnumField.RetryDisposition => CreateValidValues() with
            {
                RetryDisposition = (DaemonStartupRetryDisposition)int.MaxValue,
            },
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null),
        };

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateObservation(values));

        Assert.Equal(expectedParameterName, exception.ParamName);
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
    public void Constructor_WithNonUtcProcessStartedAt_ThrowsArgumentException ()
    {
        var values = CreateValidValues() with
        {
            ProcessStartedAtUtc = new DateTimeOffset(2026, 7, 14, 9, 0, 0, TimeSpan.FromHours(9)),
        };

        var exception = Assert.Throws<ArgumentException>(() => CreateObservation(values));

        Assert.Equal("ProcessStartedAtUtc", exception.ParamName);
    }

    private static DaemonStartStartupProgressObservation CreateObservation (ProgressObservationValues values)
    {
        return new DaemonStartStartupProgressObservation(
            values.LaunchAttemptId,
            values.EditorMode,
            values.OwnerKind,
            values.CanShutdownProcess,
            values.ProcessId,
            values.ProcessStartedAtUtc,
            values.StartupStatus,
            values.StartupBlockingReason,
            values.StartupPhase,
            values.RetryDisposition,
            values.Message,
            values.ErrorCode);
    }

    private static ProgressObservationValues CreateValidValues ()
    {
        return new ProgressObservationValues(
            Guid.Parse("01234567-89ab-cdef-0123-456789abcdef"),
            DaemonEditorMode.Batchmode,
            DaemonSessionOwnerKind.Cli,
            true,
            1234,
            new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero),
            DaemonStartupStatus.Blocked,
            DaemonStartupBlockingReason.Compile,
            DaemonDiagnosisStartupPhase.ScriptCompilation,
            DaemonStartupRetryDisposition.RetryAfterFix,
            "Compilation failed.",
            "daemon_startup_blocked");
    }

    public enum InvalidEnumField
    {
        EditorMode,
        OwnerKind,
        StartupStatus,
        StartupBlockingReason,
        StartupPhase,
        RetryDisposition,
    }

    private sealed record ProgressObservationValues (
        Guid? LaunchAttemptId,
        DaemonEditorMode? EditorMode,
        DaemonSessionOwnerKind? OwnerKind,
        bool? CanShutdownProcess,
        int? ProcessId,
        DateTimeOffset? ProcessStartedAtUtc,
        DaemonStartupStatus? StartupStatus,
        DaemonStartupBlockingReason? StartupBlockingReason,
        DaemonDiagnosisStartupPhase? StartupPhase,
        DaemonStartupRetryDisposition? RetryDisposition,
        string? Message,
        string? ErrorCode);
}
