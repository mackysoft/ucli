using MackySoft.Ucli.Application.Features.Testing.Profiles.Common.Contracts;

namespace MackySoft.Ucli.Application.Features.Testing.Profiles.UseCases.ProfileInit;

/// <summary> Implements profile initialization flow that generates test profile template JSON files. </summary>
internal sealed class TestProfileInitService : ITestProfileInitService
{
    private readonly ITestProfileTemplateStore templateStore;

    /// <summary> Initializes a new instance of the <see cref="TestProfileInitService" /> class. </summary>
    /// <param name="templateStore"> The host-owned template storage dependency. </param>
    /// <exception cref="ArgumentNullException"> Thrown when <paramref name="templateStore" /> is <see langword="null" />. </exception>
    public TestProfileInitService (ITestProfileTemplateStore templateStore)
    {
        this.templateStore = templateStore ?? throw new ArgumentNullException(nameof(templateStore));
    }

    /// <inheritdoc />
    public ValueTask<TestProfileInitExecutionResult> ExecuteAsync (
        TestProfileInitCommandInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        return templateStore.WriteAsync(
            TestProfile.CreateDefault(),
            input.OutputPath,
            input.Force,
            cancellationToken);
    }
}
