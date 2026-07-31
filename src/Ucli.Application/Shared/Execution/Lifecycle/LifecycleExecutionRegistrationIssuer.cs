using System.Diagnostics.CodeAnalysis;
using MackySoft.Ucli.Application.Shared.Identifiers;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;

namespace MackySoft.Ucli.Application.Shared.Execution.Lifecycle;

/// <summary>
/// Issues the provider-neutral identity and immutable timing of a new Lifecycle Execution.
/// </summary>
internal sealed class LifecycleExecutionRegistrationIssuer
{
    private readonly IGuidGenerator executionIdGenerator;

    private readonly TimeProvider timeProvider;

    /// <summary> Initializes one application-scoped registration issuer. </summary>
    public LifecycleExecutionRegistrationIssuer (
        IGuidGenerator executionIdGenerator,
        TimeProvider timeProvider)
    {
        this.executionIdGenerator = executionIdGenerator
            ?? throw new ArgumentNullException(nameof(executionIdGenerator));
        this.timeProvider = timeProvider
            ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    /// <summary>
    /// Issues a registration whose execution deadline is measured from the issuance time.
    /// </summary>
    /// <param name="definition"> The closed action definition. </param>
    /// <param name="executionTimeout"> The execution time available after issuance. </param>
    /// <returns> The issued registration. </returns>
    public LifecycleExecutionRegistration IssueForTimeout (
        LifecycleExecutionDefinition definition,
        TimeSpan executionTimeout)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var startedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        return new LifecycleExecutionRegistration(
            definition,
            executionIdGenerator.Generate(),
            startedAtUtc + executionTimeout,
            startedAtUtc);
    }

    /// <summary>
    /// Attempts to issue a registration against an absolute deadline whose budget may already
    /// have been consumed by application work.
    /// </summary>
    /// <param name="definition"> The closed action definition. </param>
    /// <param name="deadlineUtc"> The existing immutable execution deadline. </param>
    /// <param name="registration">
    /// The issued registration, or <see langword="null" /> when the deadline has elapsed.
    /// </param>
    /// <returns>
    /// <see langword="true" /> when a positive execution budget remains and the registration was
    /// issued; otherwise, <see langword="false" />.
    /// </returns>
    public bool TryIssueBeforeDeadline (
        LifecycleExecutionDefinition definition,
        DateTimeOffset deadlineUtc,
        [NotNullWhen(true)] out LifecycleExecutionRegistration? registration)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var startedAtUtc = timeProvider.GetUtcNow().ToUniversalTime();
        if (deadlineUtc <= startedAtUtc)
        {
            registration = null;
            return false;
        }

        registration = new LifecycleExecutionRegistration(
            definition,
            executionIdGenerator.Generate(),
            deadlineUtc,
            startedAtUtc);
        return true;
    }
}
