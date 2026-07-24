using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

namespace MackySoft.Ucli.Application.Tests;

internal static class DaemonServiceOutputAssert
{
    public static void SessionMatches (DaemonSession expected, DaemonSessionOutput? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.ProjectFingerprint, actual.ProjectFingerprint);
        Assert.Equal(expected.IssuedAtUtc, actual.IssuedAtUtc);
        Assert.Equal(expected.EditorMode, actual.EditorMode);
        Assert.Equal(expected.OwnerKind, actual.OwnerKind);
        Assert.Equal(expected.CanShutdownProcess, actual.CanShutdownProcess);
        Assert.Equal(expected.EndpointContract.TransportKind, actual.EndpointTransportKind);
        Assert.Equal(expected.EndpointContract.Address, actual.EndpointAddress);
        Assert.Equal(expected.ProcessId, actual.ProcessId);
        Assert.Equal(expected.ProcessStartedAtUtc, actual.ProcessStartedAtUtc);
        Assert.Equal(expected.OwnerProcessId, actual.OwnerProcessId);
    }

    public static void DiagnosisMatches (DaemonDiagnosis expected, DaemonDiagnosisOutput? actual)
    {
        Assert.NotNull(actual);
        Assert.Equal(expected.Reason, actual.Reason);
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(expected.ReportedBy, actual.ReportedBy);
        Assert.Equal(expected.IsInferred, actual.IsInferred);
        Assert.Equal(expected.UpdatedAtUtc, actual.UpdatedAtUtc);
        Assert.Equal(expected.ProcessId, actual.ProcessId);
        Assert.Equal(expected.EditorInstancePath?.Value, actual.EditorInstancePath);
        Assert.Equal(expected.ProcessStartedAtUtc, actual.ProcessStartedAtUtc);
        Assert.Equal(expected.UnityLogPath?.Value, actual.UnityLogPath);
        Assert.Equal(expected.StartupPhase, actual.StartupPhase);
        Assert.Equal(expected.ActionRequired, actual.ActionRequired);
        if (expected.PrimaryDiagnostic == null)
        {
            Assert.Null(actual.PrimaryDiagnostic);
        }
        else
        {
            Assert.NotNull(actual.PrimaryDiagnostic);
            Assert.Equal(expected.PrimaryDiagnostic.Kind, actual.PrimaryDiagnostic.Kind);
            Assert.Equal(expected.PrimaryDiagnostic.Code, actual.PrimaryDiagnostic.Code);
            Assert.Equal(expected.PrimaryDiagnostic.File, actual.PrimaryDiagnostic.File);
            Assert.Equal(expected.PrimaryDiagnostic.Line, actual.PrimaryDiagnostic.Line);
            Assert.Equal(expected.PrimaryDiagnostic.Column, actual.PrimaryDiagnostic.Column);
            Assert.Equal(expected.PrimaryDiagnostic.Message, actual.PrimaryDiagnostic.Message);
        }
    }
}
