using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

internal static class IpcRequestContractViolationClassifier
{
    public static IpcRequestContractViolation Classify (in IpcRequestContractReadError readError)
    {
        return readError.Kind switch
        {
            IpcRequestContractReadErrorKind.RequestMustBeObject => Create(readError, IpcRequestContractViolationKind.RequestMustBeObject),
            IpcRequestContractReadErrorKind.UnknownRequestProperty => Create(readError, IpcRequestContractViolationKind.UnknownRequestProperty),
            IpcRequestContractReadErrorKind.ProtocolVersionMissing => Create(readError, IpcRequestContractViolationKind.ProtocolVersionMissing),
            IpcRequestContractReadErrorKind.ProtocolVersionTypeMismatch => Create(readError, IpcRequestContractViolationKind.ProtocolVersionTypeMismatch),
            IpcRequestContractReadErrorKind.RequestIdContractViolation => ClassifyRequestIdContractViolation(readError),
            IpcRequestContractReadErrorKind.RequestIdFormatMismatch => Create(readError, IpcRequestContractViolationKind.RequestIdFormatMismatch),
            IpcRequestContractReadErrorKind.StepsMissing => Create(readError, IpcRequestContractViolationKind.StepsMissing),
            IpcRequestContractReadErrorKind.StepsTypeMismatch => Create(readError, IpcRequestContractViolationKind.StepsTypeMismatch),
            IpcRequestContractReadErrorKind.StepMustBeObject => Create(readError, IpcRequestContractViolationKind.StepMustBeObject),
            IpcRequestContractReadErrorKind.StepKindContractViolation => ClassifyStepKindContractViolation(readError),
            IpcRequestContractReadErrorKind.StepKindUnsupported => Create(readError, IpcRequestContractViolationKind.StepKindUnsupported),
            IpcRequestContractReadErrorKind.UnknownStepProperty => Create(readError, IpcRequestContractViolationKind.UnknownStepProperty),
            IpcRequestContractReadErrorKind.StepIdContractViolation => ClassifyStepIdContractViolation(readError),
            IpcRequestContractReadErrorKind.StepOpContractViolation => ClassifyStepOpContractViolation(readError),
            IpcRequestContractReadErrorKind.StepArgsContractViolation => ClassifyPropertyViolation(
                readError,
                IpcRequestContractViolationKind.StepArgsMissing,
                IpcRequestContractViolationKind.StepArgsTypeMismatch),
            IpcRequestContractReadErrorKind.StepOnContractViolation => ClassifyPropertyViolation(
                readError,
                IpcRequestContractViolationKind.StepOnMissing,
                IpcRequestContractViolationKind.StepOnTypeMismatch),
            IpcRequestContractReadErrorKind.StepSelectContractViolation => ClassifyPropertyViolation(
                readError,
                IpcRequestContractViolationKind.StepSelectMissing,
                IpcRequestContractViolationKind.StepSelectTypeMismatch),
            IpcRequestContractReadErrorKind.StepActionsContractViolation => ClassifyPropertyViolation(
                readError,
                IpcRequestContractViolationKind.StepActionsMissing,
                IpcRequestContractViolationKind.StepActionsTypeMismatch),
            IpcRequestContractReadErrorKind.StepActionMustBeObject => Create(readError, IpcRequestContractViolationKind.StepActionMustBeObject),
            IpcRequestContractReadErrorKind.StepCommitContractViolation => ClassifyStepCommitContractViolation(readError),
            IpcRequestContractReadErrorKind.DuplicatedStepId => Create(readError, IpcRequestContractViolationKind.DuplicatedStepId),
            _ => Create(readError, IpcRequestContractViolationKind.Unknown),
        };
    }

    private static IpcRequestContractViolation ClassifyRequestIdContractViolation (in IpcRequestContractReadError readError)
    {
        return readError.JsonStringReadError.Kind switch
        {
            JsonStringReadErrorKind.Missing => Create(readError, IpcRequestContractViolationKind.RequestIdMissing),
            JsonStringReadErrorKind.TypeMismatch => Create(readError, IpcRequestContractViolationKind.RequestIdTypeMismatch),
            JsonStringReadErrorKind.EmptyOrWhitespace => Create(readError, IpcRequestContractViolationKind.RequestIdEmptyOrWhitespace),
            JsonStringReadErrorKind.OuterWhitespace => Create(readError, IpcRequestContractViolationKind.RequestIdOuterWhitespace),
            _ => Create(readError, IpcRequestContractViolationKind.Unknown),
        };
    }

    private static IpcRequestContractViolation ClassifyStepKindContractViolation (in IpcRequestContractReadError readError)
    {
        return readError.JsonStringReadError.Kind switch
        {
            JsonStringReadErrorKind.Missing => Create(readError, IpcRequestContractViolationKind.StepKindMissing),
            JsonStringReadErrorKind.TypeMismatch => Create(readError, IpcRequestContractViolationKind.StepKindTypeMismatch),
            JsonStringReadErrorKind.EmptyOrWhitespace => Create(readError, IpcRequestContractViolationKind.StepKindEmptyOrWhitespace),
            JsonStringReadErrorKind.OuterWhitespace => Create(readError, IpcRequestContractViolationKind.StepKindOuterWhitespace),
            _ => Create(readError, IpcRequestContractViolationKind.Unknown),
        };
    }

    private static IpcRequestContractViolation ClassifyStepIdContractViolation (in IpcRequestContractReadError readError)
    {
        return readError.JsonStringReadError.Kind switch
        {
            JsonStringReadErrorKind.Missing => Create(readError, IpcRequestContractViolationKind.StepIdMissing),
            JsonStringReadErrorKind.TypeMismatch => Create(readError, IpcRequestContractViolationKind.StepIdTypeMismatch),
            JsonStringReadErrorKind.EmptyOrWhitespace => Create(readError, IpcRequestContractViolationKind.StepIdEmptyOrWhitespace),
            JsonStringReadErrorKind.OuterWhitespace => Create(readError, IpcRequestContractViolationKind.StepIdOuterWhitespace),
            _ => Create(readError, IpcRequestContractViolationKind.Unknown),
        };
    }

    private static IpcRequestContractViolation ClassifyStepOpContractViolation (in IpcRequestContractReadError readError)
    {
        return readError.JsonStringReadError.Kind switch
        {
            JsonStringReadErrorKind.Missing => Create(readError, IpcRequestContractViolationKind.StepOpMissing),
            JsonStringReadErrorKind.TypeMismatch => Create(readError, IpcRequestContractViolationKind.StepOpTypeMismatch),
            JsonStringReadErrorKind.EmptyOrWhitespace => Create(readError, IpcRequestContractViolationKind.StepOpEmptyOrWhitespace),
            JsonStringReadErrorKind.OuterWhitespace => Create(readError, IpcRequestContractViolationKind.StepOpOuterWhitespace),
            _ => Create(readError, IpcRequestContractViolationKind.Unknown),
        };
    }

    private static IpcRequestContractViolation ClassifyStepCommitContractViolation (in IpcRequestContractReadError readError)
    {
        return readError.JsonStringReadError.Kind switch
        {
            JsonStringReadErrorKind.Missing => Create(readError, IpcRequestContractViolationKind.StepCommitMissing),
            JsonStringReadErrorKind.TypeMismatch => Create(readError, IpcRequestContractViolationKind.StepCommitTypeMismatch),
            JsonStringReadErrorKind.EmptyOrWhitespace => Create(readError, IpcRequestContractViolationKind.StepCommitEmptyOrWhitespace),
            JsonStringReadErrorKind.OuterWhitespace => Create(readError, IpcRequestContractViolationKind.StepCommitOuterWhitespace),
            _ => Create(readError, IpcRequestContractViolationKind.Unknown),
        };
    }

    private static IpcRequestContractViolation ClassifyPropertyViolation (
        in IpcRequestContractReadError readError,
        IpcRequestContractViolationKind missingKind,
        IpcRequestContractViolationKind typeMismatchKind)
    {
        return readError.StepPropertyReadErrorKind switch
        {
            StepPropertyReadErrorKind.Missing => Create(readError, missingKind),
            StepPropertyReadErrorKind.TypeMismatch => Create(readError, typeMismatchKind),
            _ => Create(readError, IpcRequestContractViolationKind.Unknown),
        };
    }

    private static IpcRequestContractViolation Create (
        in IpcRequestContractReadError readError,
        IpcRequestContractViolationKind violationKind)
    {
        return new IpcRequestContractViolation(
            Kind: violationKind,
            StepIndex: readError.StepIndex,
            StepId: readError.StepId,
            UnknownPropertyName: readError.UnknownPropertyName,
            PropertyPath: readError.JsonStringReadError.PropertyName,
            DuplicatedStepId: readError.DuplicatedStepId);
    }
}
