using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Features.Init.Ports;

/// <summary> Persists init template files through a host-owned storage adapter. </summary>
internal interface IInitTemplateStore
{
    /// <summary> Writes init templates and returns generated file paths or a structured storage error. </summary>
    /// <param name="config"> The default configuration to persist. </param>
    /// <param name="force"> Whether existing template files can be overwritten. </param>
    /// <param name="cancellationToken"> The cancellation token propagated by command execution. </param>
    /// <returns> A task that resolves to the init execution result. </returns>
    ValueTask<InitExecutionResult> WriteAsync (
        UcliConfig config,
        bool force,
        CancellationToken cancellationToken = default);
}
