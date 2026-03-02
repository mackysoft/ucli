namespace MackySoft.Ucli.Contracts.Ipc.Validation;

/// <summary> Classifies request-contract read errors into normalized violation kinds. </summary>
internal static class IpcRequestContractViolationClassifier
{
    /// <summary> Classifies one request-contract read error. </summary>
    /// <param name="readError"> The request-contract read error. </param>
    /// <returns> The normalized violation value. </returns>
    public static IpcRequestContractViolation Classify (in IpcRequestContractReadError readError)
    {
        switch (readError.Kind)
        {
            case IpcRequestContractReadErrorKind.None:
                return IpcRequestContractViolation.None;
            case IpcRequestContractReadErrorKind.RequestMustBeObject:
                return Create(readError, IpcRequestContractViolationKind.RequestMustBeObject);
            case IpcRequestContractReadErrorKind.UnknownRequestProperty:
                return Create(readError, IpcRequestContractViolationKind.UnknownRequestProperty);
            case IpcRequestContractReadErrorKind.ProtocolVersionMissing:
                return Create(readError, IpcRequestContractViolationKind.ProtocolVersionMissing);
            case IpcRequestContractReadErrorKind.ProtocolVersionTypeMismatch:
                return Create(readError, IpcRequestContractViolationKind.ProtocolVersionTypeMismatch);
            case IpcRequestContractReadErrorKind.RequestIdFormatMismatch:
                return Create(readError, IpcRequestContractViolationKind.RequestIdFormatMismatch);
            case IpcRequestContractReadErrorKind.OperationsMissing:
                return Create(readError, IpcRequestContractViolationKind.OperationsMissing);
            case IpcRequestContractReadErrorKind.OperationsTypeMismatch:
                return Create(readError, IpcRequestContractViolationKind.OperationsTypeMismatch);
            case IpcRequestContractReadErrorKind.OperationMustBeObject:
                return Create(readError, IpcRequestContractViolationKind.OperationMustBeObject);
            case IpcRequestContractReadErrorKind.UnknownOperationProperty:
                return Create(readError, IpcRequestContractViolationKind.UnknownOperationProperty);
            case IpcRequestContractReadErrorKind.DuplicatedOperationId:
                return Create(readError, IpcRequestContractViolationKind.DuplicatedOperationId);
            case IpcRequestContractReadErrorKind.RequestIdContractViolation:
                return ClassifyRequestIdContractViolation(readError);
            case IpcRequestContractReadErrorKind.OperationIdContractViolation:
                return ClassifyOperationIdContractViolation(readError);
            case IpcRequestContractReadErrorKind.OperationNameContractViolation:
                return ClassifyOperationNameContractViolation(readError);
            case IpcRequestContractReadErrorKind.OperationArgsContractViolation:
                return ClassifyOperationArgsContractViolation(readError);
            case IpcRequestContractReadErrorKind.OperationAliasContractViolation:
                return ClassifyOperationAliasContractViolation(readError);
            case IpcRequestContractReadErrorKind.OperationExpectationContractViolation:
                return ClassifyOperationExpectationContractViolation(readError);
            default:
                return Create(readError, IpcRequestContractViolationKind.Unknown);
        }
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

    private static IpcRequestContractViolation ClassifyOperationIdContractViolation (in IpcRequestContractReadError readError)
    {
        return readError.JsonStringReadError.Kind switch
        {
            JsonStringReadErrorKind.Missing => Create(readError, IpcRequestContractViolationKind.OperationIdMissing),
            JsonStringReadErrorKind.TypeMismatch => Create(readError, IpcRequestContractViolationKind.OperationIdTypeMismatch),
            JsonStringReadErrorKind.EmptyOrWhitespace => Create(readError, IpcRequestContractViolationKind.OperationIdEmptyOrWhitespace),
            JsonStringReadErrorKind.OuterWhitespace => Create(readError, IpcRequestContractViolationKind.OperationIdOuterWhitespace),
            _ => Create(readError, IpcRequestContractViolationKind.Unknown),
        };
    }

    private static IpcRequestContractViolation ClassifyOperationNameContractViolation (in IpcRequestContractReadError readError)
    {
        return readError.JsonStringReadError.Kind switch
        {
            JsonStringReadErrorKind.Missing => Create(readError, IpcRequestContractViolationKind.OperationNameMissing),
            JsonStringReadErrorKind.TypeMismatch => Create(readError, IpcRequestContractViolationKind.OperationNameTypeMismatch),
            JsonStringReadErrorKind.EmptyOrWhitespace => Create(readError, IpcRequestContractViolationKind.OperationNameEmptyOrWhitespace),
            JsonStringReadErrorKind.OuterWhitespace => Create(readError, IpcRequestContractViolationKind.OperationNameOuterWhitespace),
            _ => Create(readError, IpcRequestContractViolationKind.Unknown),
        };
    }

    private static IpcRequestContractViolation ClassifyOperationArgsContractViolation (in IpcRequestContractReadError readError)
    {
        return readError.OperationObjectReadErrorKind switch
        {
            OperationObjectReadErrorKind.Missing => Create(readError, IpcRequestContractViolationKind.OperationArgsMissing),
            OperationObjectReadErrorKind.TypeMismatch => Create(readError, IpcRequestContractViolationKind.OperationArgsTypeMismatch),
            _ => Create(readError, IpcRequestContractViolationKind.Unknown),
        };
    }

    private static IpcRequestContractViolation ClassifyOperationAliasContractViolation (in IpcRequestContractReadError readError)
    {
        return readError.JsonStringReadError.Kind switch
        {
            JsonStringReadErrorKind.TypeMismatch => Create(readError, IpcRequestContractViolationKind.OperationAliasTypeMismatch),
            JsonStringReadErrorKind.EmptyOrWhitespace => Create(readError, IpcRequestContractViolationKind.OperationAliasEmptyOrWhitespace),
            JsonStringReadErrorKind.OuterWhitespace => Create(readError, IpcRequestContractViolationKind.OperationAliasOuterWhitespace),
            _ => Create(readError, IpcRequestContractViolationKind.Unknown),
        };
    }

    private static IpcRequestContractViolation ClassifyOperationExpectationContractViolation (in IpcRequestContractReadError readError)
    {
        return readError.ExpectationReadError.Kind switch
        {
            ExpectationConstraintReadErrorKind.ExpectationMustBeObject => Create(readError, IpcRequestContractViolationKind.ExpectationMustBeObject),
            ExpectationConstraintReadErrorKind.ExpectationContainsUnknownProperty => Create(readError, IpcRequestContractViolationKind.ExpectationContainsUnknownProperty),
            ExpectationConstraintReadErrorKind.ExpectationMustContainAtLeastOneConstraint => Create(readError, IpcRequestContractViolationKind.ExpectationMustContainAtLeastOneConstraint),
            ExpectationConstraintReadErrorKind.BooleanConstraintMustBeBoolean => Create(readError, IpcRequestContractViolationKind.ExpectationBooleanConstraintMustBeBoolean),
            ExpectationConstraintReadErrorKind.IntegerConstraintMustBeInteger => Create(readError, IpcRequestContractViolationKind.ExpectationIntegerConstraintMustBeInteger),
            ExpectationConstraintReadErrorKind.IntegerConstraintMustBeNonNegative => Create(readError, IpcRequestContractViolationKind.ExpectationIntegerConstraintMustBeNonNegative),
            ExpectationConstraintReadErrorKind.CountCannotCombineWithMinOrMax => Create(readError, IpcRequestContractViolationKind.ExpectationCountCannotCombineWithMinOrMax),
            ExpectationConstraintReadErrorKind.MinMustBeLessThanOrEqualToMax => Create(readError, IpcRequestContractViolationKind.ExpectationMinMustBeLessThanOrEqualToMax),
            _ => Create(readError, IpcRequestContractViolationKind.Unknown),
        };
    }

    private static IpcRequestContractViolation Create (
        in IpcRequestContractReadError readError,
        IpcRequestContractViolationKind violationKind)
    {
        return new IpcRequestContractViolation(
            Kind: violationKind,
            OperationIndex: readError.OperationIndex,
            OperationId: readError.OperationId,
            UnknownPropertyName: readError.UnknownPropertyName ?? readError.ExpectationReadError.UnknownPropertyName,
            PropertyPath: readError.ExpectationReadError.PropertyPath,
            DuplicatedOperationId: readError.DuplicatedOperationId);
    }
}