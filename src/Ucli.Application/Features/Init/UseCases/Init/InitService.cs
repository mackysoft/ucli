using MackySoft.Ucli.Application.Features.Init.Common.Contracts;
using MackySoft.Ucli.Application.Shared.Configuration;

namespace MackySoft.Ucli.Application.Features.Init.UseCases.Init;

/// <summary> Implements init flow that generates explicit <c>.ucli</c> config template files. </summary>
internal sealed class InitService : IInitService
{
    private readonly IInitTemplateStore templateStore;

    /// <summary> Initializes a new instance of the <see cref="InitService" /> class. </summary>
    /// <param name="templateStore"> The host-owned template storage dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="templateStore" /> is <see langword="null" />. </exception>
    public InitService (IInitTemplateStore templateStore)
    {
        this.templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
    }

    /// <inheritdoc />
    public ValueTask<InitExecutionResult> ExecuteAsync (
        InitCommandInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        return templateStore.WriteAsync(UcliConfig.CreateDefault(), input.Force, cancellationToken);
    }
}
