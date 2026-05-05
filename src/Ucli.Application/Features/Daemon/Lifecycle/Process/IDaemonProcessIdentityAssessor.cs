using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;

/// <summary> Assesses whether one operating-system process still matches expected daemon session identity. </summary>
internal interface IDaemonProcessIdentityAssessor
{
    /// <summary> Resolves one process by identifier and assesses whether it matches expected daemon identity. </summary>
    /// <param name="processId"> The process identifier to assess. </param>
    /// <param name="expectedIssuedAtUtc"> The expected daemon session issued-at timestamp. </param>
    /// <returns> The process identity assessment result. </returns>
    DaemonProcessIdentityAssessment AssessByProcessId (
        int processId,
        DateTimeOffset expectedIssuedAtUtc);
}
