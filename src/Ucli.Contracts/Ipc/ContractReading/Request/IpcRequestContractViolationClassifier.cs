using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

internal static class IpcRequestContractViolationClassifier
{
    private static readonly IReadOnlyDictionary<IpcRequestContractReadErrorKind, IpcRequestContractViolationKind> DirectViolationKinds =
        new Dictionary<IpcRequestContractReadErrorKind, IpcRequestContractViolationKind>
        {
            [IpcRequestContractReadErrorKind.RequestMustBeObject] = IpcRequestContractViolationKind.RequestMustBeObject,
            [IpcRequestContractReadErrorKind.UnknownRequestProperty] = IpcRequestContractViolationKind.UnknownRequestProperty,
            [IpcRequestContractReadErrorKind.ProtocolVersionMissing] = IpcRequestContractViolationKind.ProtocolVersionMissing,
            [IpcRequestContractReadErrorKind.ProtocolVersionTypeMismatch] = IpcRequestContractViolationKind.ProtocolVersionTypeMismatch,
            [IpcRequestContractReadErrorKind.RequestIdFormatMismatch] = IpcRequestContractViolationKind.RequestIdFormatMismatch,
            [IpcRequestContractReadErrorKind.StepsMissing] = IpcRequestContractViolationKind.StepsMissing,
            [IpcRequestContractReadErrorKind.StepsTypeMismatch] = IpcRequestContractViolationKind.StepsTypeMismatch,
            [IpcRequestContractReadErrorKind.StepMustBeObject] = IpcRequestContractViolationKind.StepMustBeObject,
            [IpcRequestContractReadErrorKind.StepKindUnsupported] = IpcRequestContractViolationKind.StepKindUnsupported,
            [IpcRequestContractReadErrorKind.UnknownStepProperty] = IpcRequestContractViolationKind.UnknownStepProperty,
            [IpcRequestContractReadErrorKind.StepActionMustBeObject] = IpcRequestContractViolationKind.StepActionMustBeObject,
            [IpcRequestContractReadErrorKind.DuplicatedStepId] = IpcRequestContractViolationKind.DuplicatedStepId,
        };

    public static IpcRequestContractViolation Classify (in IpcRequestContractReadError readError)
    {
        if (DirectViolationKinds.TryGetValue(readError.Kind, out var violationKind))
        {
            return Create(readError, violationKind);
        }

        return readError.Kind switch
        {
            IpcRequestContractReadErrorKind.RequestIdContractViolation => ClassifyRequestIdContractViolation(readError),
            IpcRequestContractReadErrorKind.StepKindContractViolation => ClassifyStepKindContractViolation(readError),
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
            IpcRequestContractReadErrorKind.StepCommitContractViolation => ClassifyStepCommitContractViolation(readError),
            _ => Create(readError, IpcRequestContractViolationKind.Unknown),
        };
    }

    private static IpcRequestContractViolation ClassifyRequestIdContractViolation (in IpcRequestContractReadError readError)
    {
        return ClassifyJsonString(readError, RequestIdViolationKinds);
    }

    private static IpcRequestContractViolation ClassifyStepKindContractViolation (in IpcRequestContractReadError readError)
    {
        return ClassifyJsonString(readError, StepKindViolationKinds);
    }

    private static IpcRequestContractViolation ClassifyStepIdContractViolation (in IpcRequestContractReadError readError)
    {
        return ClassifyJsonString(readError, StepIdViolationKinds);
    }

    private static IpcRequestContractViolation ClassifyStepOpContractViolation (in IpcRequestContractReadError readError)
    {
        return ClassifyJsonString(readError, StepOpViolationKinds);
    }

    private static IpcRequestContractViolation ClassifyStepCommitContractViolation (in IpcRequestContractReadError readError)
    {
        return ClassifyJsonString(readError, StepCommitViolationKinds);
    }

    private static IpcRequestContractViolation ClassifyJsonString (
        in IpcRequestContractReadError readError,
        JsonStringViolationKinds kinds)
    {
        return readError.JsonStringReadError.Kind switch
        {
            JsonStringReadErrorKind.Missing => Create(readError, kinds.Missing),
            JsonStringReadErrorKind.TypeMismatch => Create(readError, kinds.TypeMismatch),
            JsonStringReadErrorKind.EmptyOrWhitespace => Create(readError, kinds.EmptyOrWhitespace),
            JsonStringReadErrorKind.OuterWhitespace => Create(readError, kinds.OuterWhitespace),
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

    private static readonly JsonStringViolationKinds RequestIdViolationKinds = new(
        IpcRequestContractViolationKind.RequestIdMissing,
        IpcRequestContractViolationKind.RequestIdTypeMismatch,
        IpcRequestContractViolationKind.RequestIdEmptyOrWhitespace,
        IpcRequestContractViolationKind.RequestIdOuterWhitespace);

    private static readonly JsonStringViolationKinds StepKindViolationKinds = new(
        IpcRequestContractViolationKind.StepKindMissing,
        IpcRequestContractViolationKind.StepKindTypeMismatch,
        IpcRequestContractViolationKind.StepKindEmptyOrWhitespace,
        IpcRequestContractViolationKind.StepKindOuterWhitespace);

    private static readonly JsonStringViolationKinds StepIdViolationKinds = new(
        IpcRequestContractViolationKind.StepIdMissing,
        IpcRequestContractViolationKind.StepIdTypeMismatch,
        IpcRequestContractViolationKind.StepIdEmptyOrWhitespace,
        IpcRequestContractViolationKind.StepIdOuterWhitespace);

    private static readonly JsonStringViolationKinds StepOpViolationKinds = new(
        IpcRequestContractViolationKind.StepOpMissing,
        IpcRequestContractViolationKind.StepOpTypeMismatch,
        IpcRequestContractViolationKind.StepOpEmptyOrWhitespace,
        IpcRequestContractViolationKind.StepOpOuterWhitespace);

    private static readonly JsonStringViolationKinds StepCommitViolationKinds = new(
        IpcRequestContractViolationKind.StepCommitMissing,
        IpcRequestContractViolationKind.StepCommitTypeMismatch,
        IpcRequestContractViolationKind.StepCommitEmptyOrWhitespace,
        IpcRequestContractViolationKind.StepCommitOuterWhitespace);

    private readonly struct JsonStringViolationKinds
    {
        public JsonStringViolationKinds (
            IpcRequestContractViolationKind missing,
            IpcRequestContractViolationKind typeMismatch,
            IpcRequestContractViolationKind emptyOrWhitespace,
            IpcRequestContractViolationKind outerWhitespace)
        {
            Missing = missing;
            TypeMismatch = typeMismatch;
            EmptyOrWhitespace = emptyOrWhitespace;
            OuterWhitespace = outerWhitespace;
        }

        public IpcRequestContractViolationKind Missing { get; }

        public IpcRequestContractViolationKind TypeMismatch { get; }

        public IpcRequestContractViolationKind EmptyOrWhitespace { get; }

        public IpcRequestContractViolationKind OuterWhitespace { get; }
    }
}
