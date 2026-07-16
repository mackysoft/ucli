using MackySoft.Ucli.Contracts.Ipc.Validation;

namespace MackySoft.Ucli.Contracts.Ipc.ContractReading;

internal static class IpcExecuteArgumentsContractViolationClassifier
{
    private static readonly IReadOnlyDictionary<IpcExecuteArgumentsContractReadErrorKind, IpcExecuteArgumentsContractViolationKind> DirectViolationKinds =
        new Dictionary<IpcExecuteArgumentsContractReadErrorKind, IpcExecuteArgumentsContractViolationKind>
        {
            [IpcExecuteArgumentsContractReadErrorKind.ArgumentsMustBeObject] = IpcExecuteArgumentsContractViolationKind.ArgumentsMustBeObject,
            [IpcExecuteArgumentsContractReadErrorKind.UnknownArgumentsProperty] = IpcExecuteArgumentsContractViolationKind.UnknownArgumentsProperty,
            [IpcExecuteArgumentsContractReadErrorKind.ProtocolVersionMissing] = IpcExecuteArgumentsContractViolationKind.ProtocolVersionMissing,
            [IpcExecuteArgumentsContractReadErrorKind.ProtocolVersionTypeMismatch] = IpcExecuteArgumentsContractViolationKind.ProtocolVersionTypeMismatch,
            [IpcExecuteArgumentsContractReadErrorKind.StepsMissing] = IpcExecuteArgumentsContractViolationKind.StepsMissing,
            [IpcExecuteArgumentsContractReadErrorKind.StepsTypeMismatch] = IpcExecuteArgumentsContractViolationKind.StepsTypeMismatch,
            [IpcExecuteArgumentsContractReadErrorKind.StepMustBeObject] = IpcExecuteArgumentsContractViolationKind.StepMustBeObject,
            [IpcExecuteArgumentsContractReadErrorKind.StepKindUnsupported] = IpcExecuteArgumentsContractViolationKind.StepKindUnsupported,
            [IpcExecuteArgumentsContractReadErrorKind.UnknownStepProperty] = IpcExecuteArgumentsContractViolationKind.UnknownStepProperty,
            [IpcExecuteArgumentsContractReadErrorKind.StepActionMustBeObject] = IpcExecuteArgumentsContractViolationKind.StepActionMustBeObject,
            [IpcExecuteArgumentsContractReadErrorKind.DuplicatedStepId] = IpcExecuteArgumentsContractViolationKind.DuplicatedStepId,
        };

    public static IpcExecuteArgumentsContractViolation Classify (in IpcExecuteArgumentsContractReadError readError)
    {
        if (DirectViolationKinds.TryGetValue(readError.Kind, out var violationKind))
        {
            return Create(readError, violationKind);
        }

        return readError.Kind switch
        {
            IpcExecuteArgumentsContractReadErrorKind.StepKindContractViolation => ClassifyStepKindContractViolation(readError),
            IpcExecuteArgumentsContractReadErrorKind.StepIdContractViolation => ClassifyStepIdContractViolation(readError),
            IpcExecuteArgumentsContractReadErrorKind.StepOpContractViolation => ClassifyStepOpContractViolation(readError),
            IpcExecuteArgumentsContractReadErrorKind.StepArgsContractViolation => ClassifyPropertyViolation(
                readError,
                IpcExecuteArgumentsContractViolationKind.StepArgsMissing,
                IpcExecuteArgumentsContractViolationKind.StepArgsTypeMismatch),
            IpcExecuteArgumentsContractReadErrorKind.StepOnContractViolation => ClassifyPropertyViolation(
                readError,
                IpcExecuteArgumentsContractViolationKind.StepOnMissing,
                IpcExecuteArgumentsContractViolationKind.StepOnTypeMismatch),
            IpcExecuteArgumentsContractReadErrorKind.StepSelectContractViolation => ClassifyPropertyViolation(
                readError,
                IpcExecuteArgumentsContractViolationKind.StepSelectMissing,
                IpcExecuteArgumentsContractViolationKind.StepSelectTypeMismatch),
            IpcExecuteArgumentsContractReadErrorKind.StepActionsContractViolation => ClassifyPropertyViolation(
                readError,
                IpcExecuteArgumentsContractViolationKind.StepActionsMissing,
                IpcExecuteArgumentsContractViolationKind.StepActionsTypeMismatch),
            IpcExecuteArgumentsContractReadErrorKind.StepCommitContractViolation => ClassifyStepCommitContractViolation(readError),
            _ => Create(readError, IpcExecuteArgumentsContractViolationKind.Unknown),
        };
    }

    private static IpcExecuteArgumentsContractViolation ClassifyStepKindContractViolation (in IpcExecuteArgumentsContractReadError readError)
    {
        return ClassifyJsonString(readError, StepKindViolationKinds);
    }

    private static IpcExecuteArgumentsContractViolation ClassifyStepIdContractViolation (in IpcExecuteArgumentsContractReadError readError)
    {
        return ClassifyJsonString(readError, StepIdViolationKinds);
    }

    private static IpcExecuteArgumentsContractViolation ClassifyStepOpContractViolation (in IpcExecuteArgumentsContractReadError readError)
    {
        return ClassifyJsonString(readError, StepOpViolationKinds);
    }

    private static IpcExecuteArgumentsContractViolation ClassifyStepCommitContractViolation (in IpcExecuteArgumentsContractReadError readError)
    {
        return ClassifyJsonString(readError, StepCommitViolationKinds);
    }

    private static IpcExecuteArgumentsContractViolation ClassifyJsonString (
        in IpcExecuteArgumentsContractReadError readError,
        JsonStringViolationKinds kinds)
    {
        return readError.JsonStringReadError.Kind switch
        {
            JsonStringReadErrorKind.Missing => Create(readError, kinds.Missing),
            JsonStringReadErrorKind.TypeMismatch => Create(readError, kinds.TypeMismatch),
            JsonStringReadErrorKind.EmptyOrWhitespace => Create(readError, kinds.EmptyOrWhitespace),
            JsonStringReadErrorKind.OuterWhitespace => Create(readError, kinds.OuterWhitespace),
            _ => Create(readError, IpcExecuteArgumentsContractViolationKind.Unknown),
        };
    }

    private static IpcExecuteArgumentsContractViolation ClassifyPropertyViolation (
        in IpcExecuteArgumentsContractReadError readError,
        IpcExecuteArgumentsContractViolationKind missingKind,
        IpcExecuteArgumentsContractViolationKind typeMismatchKind)
    {
        return readError.StepPropertyReadErrorKind switch
        {
            StepPropertyReadErrorKind.Missing => Create(readError, missingKind),
            StepPropertyReadErrorKind.TypeMismatch => Create(readError, typeMismatchKind),
            _ => Create(readError, IpcExecuteArgumentsContractViolationKind.Unknown),
        };
    }

    private static IpcExecuteArgumentsContractViolation Create (
        in IpcExecuteArgumentsContractReadError readError,
        IpcExecuteArgumentsContractViolationKind violationKind)
    {
        return new IpcExecuteArgumentsContractViolation(
            Kind: violationKind,
            StepIndex: readError.StepIndex,
            StepId: readError.StepId,
            UnknownPropertyName: readError.UnknownPropertyName,
            PropertyPath: readError.JsonStringReadError.PropertyName,
            DuplicatedStepId: readError.DuplicatedStepId);
    }

    private static readonly JsonStringViolationKinds StepKindViolationKinds = new(
        IpcExecuteArgumentsContractViolationKind.StepKindMissing,
        IpcExecuteArgumentsContractViolationKind.StepKindTypeMismatch,
        IpcExecuteArgumentsContractViolationKind.StepKindEmptyOrWhitespace,
        IpcExecuteArgumentsContractViolationKind.StepKindOuterWhitespace);

    private static readonly JsonStringViolationKinds StepIdViolationKinds = new(
        IpcExecuteArgumentsContractViolationKind.StepIdMissing,
        IpcExecuteArgumentsContractViolationKind.StepIdTypeMismatch,
        IpcExecuteArgumentsContractViolationKind.StepIdEmptyOrWhitespace,
        IpcExecuteArgumentsContractViolationKind.StepIdOuterWhitespace);

    private static readonly JsonStringViolationKinds StepOpViolationKinds = new(
        IpcExecuteArgumentsContractViolationKind.StepOpMissing,
        IpcExecuteArgumentsContractViolationKind.StepOpTypeMismatch,
        IpcExecuteArgumentsContractViolationKind.StepOpEmptyOrWhitespace,
        IpcExecuteArgumentsContractViolationKind.StepOpOuterWhitespace);

    private static readonly JsonStringViolationKinds StepCommitViolationKinds = new(
        IpcExecuteArgumentsContractViolationKind.StepCommitMissing,
        IpcExecuteArgumentsContractViolationKind.StepCommitTypeMismatch,
        IpcExecuteArgumentsContractViolationKind.StepCommitEmptyOrWhitespace,
        IpcExecuteArgumentsContractViolationKind.StepCommitOuterWhitespace);

    private readonly struct JsonStringViolationKinds
    {
        public JsonStringViolationKinds (
            IpcExecuteArgumentsContractViolationKind missing,
            IpcExecuteArgumentsContractViolationKind typeMismatch,
            IpcExecuteArgumentsContractViolationKind emptyOrWhitespace,
            IpcExecuteArgumentsContractViolationKind outerWhitespace)
        {
            Missing = missing;
            TypeMismatch = typeMismatch;
            EmptyOrWhitespace = emptyOrWhitespace;
            OuterWhitespace = outerWhitespace;
        }

        public IpcExecuteArgumentsContractViolationKind Missing { get; }

        public IpcExecuteArgumentsContractViolationKind TypeMismatch { get; }

        public IpcExecuteArgumentsContractViolationKind EmptyOrWhitespace { get; }

        public IpcExecuteArgumentsContractViolationKind OuterWhitespace { get; }
    }
}
