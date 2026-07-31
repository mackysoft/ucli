using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MackySoft.Ucli.Contracts;
using MackySoft.Ucli.Contracts.Execution.Lifecycle;
using MackySoft.Ucli.Infrastructure.Execution.Lifecycle;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary>
    /// Owns terminal publication, recovery, common result classification, and failure logging
    /// without owning an action state machine or Terminal Record construction.
    /// </summary>
    internal sealed class LifecycleExecutionTerminalPublicationBoundary<TTerminalRecord>
        where TTerminalRecord : LifecycleExecutionTerminalRecord
    {
        private readonly LifecycleExecutionKind kind;
        private readonly FileLifecycleExecutionStore executionStore;
        private readonly IDaemonLogger daemonLogger;
        private readonly string failureLogMessage;
        private readonly string recoveryFailureLogMessage;

        public LifecycleExecutionTerminalPublicationBoundary (
            LifecycleExecutionKind kind,
            FileLifecycleExecutionStore executionStore,
            IDaemonLogger daemonLogger,
            string failureLogMessage,
            string recoveryFailureLogMessage)
        {
            this.kind = kind;
            this.executionStore = executionStore
                ?? throw new ArgumentNullException(nameof(executionStore));
            this.daemonLogger = daemonLogger
                ?? throw new ArgumentNullException(nameof(daemonLogger));
            ValidateFailureLogMessage(failureLogMessage);
            ValidateFailureLogMessage(recoveryFailureLogMessage);
            this.failureLogMessage = failureLogMessage;
            this.recoveryFailureLogMessage = recoveryFailureLogMessage;
        }

        /// <summary>
        /// Creates the action-owned Terminal Record from the durable start, then fixes, publishes,
        /// and reverifies it.
        /// </summary>
        public ValueTask<LifecycleExecutionTerminalPublication<TTerminalRecord>>
            PublishAsync (
            Guid executionId,
            ExecutionRef authoritativeReconnectableReference,
            Func<LifecycleExecutionStartBinding, TTerminalRecord>
                terminalRecordFactory,
            CancellationToken cancellationToken)
        {
            return PublishCoreAsync(
                executionId,
                authoritativeReconnectableReference,
                terminalRecordFactory,
                failureLogMessage,
                cancellationToken);
        }

        /// <summary>
        /// Attempts terminal publication during host recovery. A publication failure remains
        /// durable and reconnectable, and is logged with recovery context.
        /// </summary>
        public async ValueTask TryPublishDuringRecoveryAsync (
            Guid executionId,
            ExecutionRef authoritativeReconnectableReference,
            Func<LifecycleExecutionStartBinding, TTerminalRecord>
                terminalRecordFactory,
            CancellationToken cancellationToken)
        {
            _ = await PublishCoreAsync(
                executionId,
                authoritativeReconnectableReference,
                terminalRecordFactory,
                recoveryFailureLogMessage,
                cancellationToken);
        }

        private async ValueTask<
            LifecycleExecutionTerminalPublication<TTerminalRecord>>
            PublishCoreAsync (
            Guid executionId,
            ExecutionRef authoritativeReconnectableReference,
            Func<LifecycleExecutionStartBinding, TTerminalRecord>
                terminalRecordFactory,
            string publicationFailureLogMessage,
            CancellationToken cancellationToken)
        {
            if (executionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Lifecycle Execution identifier must not be empty.",
                    nameof(executionId));
            }
            LifecycleExecutionContractGuard.RequireReference(
                authoritativeReconnectableReference,
                nameof(authoritativeReconnectableReference),
                kind,
                allowTerminal: false);
            if (authoritativeReconnectableReference
                    is not IReconnectableExecutionRef
                || authoritativeReconnectableReference.Id != executionId)
            {
                throw new ArgumentException(
                    "Terminal publication requires a reconnectable reference for the requested Lifecycle Execution.",
                    nameof(authoritativeReconnectableReference));
            }
            if (terminalRecordFactory == null)
            {
                throw new ArgumentNullException(nameof(terminalRecordFactory));
            }

            var fallbackReference = CreatePublishingRecoveryProjection(
                authoritativeReconnectableReference);
            try
            {
                var stored = await executionStore.ReadAsync(
                    kind,
                    executionId,
                    cancellationToken);
                if (stored == null)
                {
                    return Classify(
                        new LifecycleExecutionTerminalPublicationResult(
                            LifecycleExecutionTerminalPublicationOutcome.Missing,
                            TerminalReference: null,
                            TerminalRecord: null),
                        publicationFailureLogMessage,
                        fallbackReference);
                }

                LifecycleExecutionTerminalPublicationResult publication;
                if (stored.IsTerminal || stored.IsPublishing)
                {
                    publication =
                        await executionStore.TryRecoverTerminalPublicationAsync(
                            kind,
                            executionId,
                            cancellationToken);
                }
                else
                {
                    var terminalRecord = terminalRecordFactory(stored.Start)
                        ?? throw new InvalidOperationException(
                            "The action Terminal Record factory returned no record.");
                    publication = await executionStore.PublishTerminalAsync(
                        terminalRecord,
                        cancellationToken);
                }

                return Classify(
                    publication,
                    publicationFailureLogMessage,
                    fallbackReference);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return await CreateUnavailableAsync(
                    executionId,
                    publicationFailureLogMessage,
                    exception,
                    fallbackReference);
            }
        }

        /// <summary>
        /// Completes or reverifies the fixed Terminal Record for one existing execution.
        /// </summary>
        public ValueTask<LifecycleExecutionTerminalPublication<TTerminalRecord>>
            RecoverAsync (
            Guid executionId,
            ExecutionRef authoritativeExecutionReference,
            CancellationToken cancellationToken)
        {
            return RecoverCoreAsync(
                executionId,
                authoritativeExecutionReference,
                failureLogMessage,
                cancellationToken);
        }

        /// <summary>
        /// Attempts to complete or reverify a fixed Terminal Record during host recovery.
        /// </summary>
        public async ValueTask TryRecoverDuringRecoveryAsync (
            Guid executionId,
            ExecutionRef authoritativeExecutionReference,
            CancellationToken cancellationToken)
        {
            _ = await RecoverCoreAsync(
                executionId,
                authoritativeExecutionReference,
                recoveryFailureLogMessage,
                cancellationToken);
        }

        private async ValueTask<
            LifecycleExecutionTerminalPublication<TTerminalRecord>>
            RecoverCoreAsync (
            Guid executionId,
            ExecutionRef authoritativeExecutionReference,
            string publicationFailureLogMessage,
            CancellationToken cancellationToken)
        {
            if (executionId == Guid.Empty)
            {
                throw new ArgumentException(
                    "Lifecycle Execution identifier must not be empty.",
                    nameof(executionId));
            }
            LifecycleExecutionContractGuard.RequireReference(
                authoritativeExecutionReference,
                nameof(authoritativeExecutionReference),
                kind);
            if (authoritativeExecutionReference.Id != executionId)
            {
                throw new ArgumentException(
                    "Terminal recovery requires an authoritative reference for the requested Lifecycle Execution.",
                    nameof(authoritativeExecutionReference));
            }

            var fallbackReference = CreatePublishingRecoveryProjection(
                authoritativeExecutionReference);
            try
            {
                var publication =
                    await executionStore.TryRecoverTerminalPublicationAsync(
                        kind,
                        executionId,
                        cancellationToken);
                return Classify(
                    publication,
                    publicationFailureLogMessage,
                    fallbackReference);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                return await CreateUnavailableAsync(
                    executionId,
                    publicationFailureLogMessage,
                    exception,
                    fallbackReference);
            }
        }

        private LifecycleExecutionTerminalPublication<TTerminalRecord>
            Classify (
            LifecycleExecutionTerminalPublicationResult publication,
            string failureLogMessage,
            ExecutionRef fallbackReference)
        {
            if (publication == null)
            {
                throw new ArgumentNullException(nameof(publication));
            }

            if ((publication.Outcome
                    is LifecycleExecutionTerminalPublicationOutcome.Published
                        or LifecycleExecutionTerminalPublicationOutcome.Reconnected)
                && publication.TerminalReference != null
                && publication.TerminalRecord
                    is TTerminalRecord terminalRecord)
            {
                return new LifecycleExecutionTerminalPublication<
                    TTerminalRecord>.Verified(
                    publication.TerminalReference,
                    terminalRecord);
            }

            if (publication.Outcome
                    == LifecycleExecutionTerminalPublicationOutcome
                        .PublicationFailed
                && publication.TerminalReference == null
                && publication.ReconnectableReference != null
                && publication.TerminalRecord
                    is TTerminalRecord fixedTerminalRecord)
            {
                if (publication.Failure != null)
                {
                    LogFailure(failureLogMessage, publication.Failure);
                }

                return new LifecycleExecutionTerminalPublication<
                    TTerminalRecord>.PublicationFailed(
                    publication.ReconnectableReference,
                    fixedTerminalRecord);
            }

            var failure = publication.Failure
                ?? new IOException(
                    $"Lifecycle Execution terminal publication returned incomplete outcome '{publication.Outcome}'.");
            LogFailure(failureLogMessage, failure);
            var reconnectableReference =
                publication.ReconnectableReference
                ?? LifecycleExecutionReferenceFactory
                    .CreateTerminalPublicationFailureProjection(
                        publication.AuthoritativeExecution)
                ?? fallbackReference;
            reconnectableReference = CreatePublishingRecoveryProjection(
                reconnectableReference);
            return new LifecycleExecutionTerminalPublication<
                TTerminalRecord>.Unavailable(
                reconnectableReference);
        }

        private async ValueTask<
            LifecycleExecutionTerminalPublication<TTerminalRecord>>
            CreateUnavailableAsync (
            Guid executionId,
            string failureLogMessage,
            Exception publicationFailure,
            ExecutionRef fallbackReference)
        {
            Exception failure = publicationFailure;
            ExecutionRef reconnectableReference = null;
            try
            {
                var stored = await executionStore.ReadAsync(
                    kind,
                    executionId,
                    CancellationToken.None);
                reconnectableReference =
                    LifecycleExecutionReferenceFactory
                        .CreateTerminalPublicationFailureProjection(stored);
                reconnectableReference = CreatePublishingRecoveryProjection(
                    reconnectableReference ?? fallbackReference);
            }
            catch (Exception projectionFailure)
            {
                failure = new AggregateException(
                    publicationFailure,
                    projectionFailure);
                reconnectableReference = fallbackReference;
            }

            LogFailure(failureLogMessage, failure);
            return new LifecycleExecutionTerminalPublication<
                TTerminalRecord>.Unavailable(
                reconnectableReference);
        }

        private static ExecutionRef CreatePublishingRecoveryProjection (
            ExecutionRef reconnectableReference)
        {
            return LifecycleExecutionReferenceFactory.CreateStateProjection(
                reconnectableReference
                    ?? throw new ArgumentNullException(
                        nameof(reconnectableReference)),
                ExecutionLifecycle.Recovery,
                LifecycleExecutionState.Publishing);
        }

        private void LogFailure (
            string failureLogMessage,
            Exception failure)
        {
            daemonLogger.Exception(
                DaemonLogCategories.Lifecycle,
                failureLogMessage,
                failure);
        }

        private static void ValidateFailureLogMessage (string failureLogMessage)
        {
            if (string.IsNullOrWhiteSpace(failureLogMessage))
            {
                throw new ArgumentException(
                    "Terminal publication failure log message must not be empty.",
                    nameof(failureLogMessage));
            }
        }
    }
}
