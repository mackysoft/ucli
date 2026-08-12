using MackySoft.Ucli.Application.Features.Programs.Persistence;

namespace MackySoft.Ucli.Application.Features.Programs.Supervision;

/// <summary> Publishes the durable Run creation notice before the first Program Step may start. </summary>
internal interface IProgramRunStartNotificationPort
{
    ValueTask NotifyAsync (ProgramRunRecord run, CancellationToken cancellationToken = default);
}
