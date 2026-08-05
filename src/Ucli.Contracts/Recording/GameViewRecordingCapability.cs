using System.Text.Json.Serialization;
using MackySoft.Ucli.Contracts.Operations;

namespace MackySoft.Ucli.Contracts.Recording;

/// <summary>Describes the resolved Recorder package observation.</summary>
public sealed record GameViewRecordingPackageCapability
{
    [JsonConstructor]
    public GameViewRecordingPackageCapability (
        GameViewRecordingPackageState state,
        string packageId,
        string? version)
    {
        EnsureDefined(state, nameof(state));
        if (!string.Equals(packageId, GameViewRecorderCompatibilityMetadata.PackageId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Recorder package id must match the bundled compatibility metadata.", nameof(packageId));
        }

        if ((state == GameViewRecordingPackageState.Resolved) != (version is not null))
        {
            throw new ArgumentException("A resolved package state must carry exactly one package version.", nameof(version));
        }

        State = state;
        PackageId = packageId;
        Version = version is null ? null : RequireTrimmedValue(version, nameof(version));
    }

    public GameViewRecordingPackageState State { get; }

    public string PackageId { get; }

    public string? Version { get; }

    private static void EnsureDefined (GameViewRecordingPackageState state, string parameterName)
    {
        if (!TextVocabulary.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(parameterName, state, "Recorder package state must be defined.");
        }
    }

    private static string RequireTrimmedValue (string value, string parameterName)
    {
        var required = ContractArgumentGuard.RequireValue(value, parameterName);
        if (!string.Equals(required, required.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("Value must not contain outer whitespace.", parameterName);
        }

        return required;
    }
}

/// <summary>Describes compatibility between a resolved Recorder package and the bundled adapter.</summary>
public sealed record GameViewRecordingCompatibilityCapability
{
    [JsonConstructor]
    public GameViewRecordingCompatibilityCapability (
        GameViewRecordingCompatibilityState state,
        string recorderPackageVersionRange,
        string? resolvedVersion)
    {
        if (!TextVocabulary.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Recorder compatibility state must be defined.");
        }

        if (!string.Equals(
            recorderPackageVersionRange,
            GameViewRecorderCompatibilityMetadata.RecorderPackageVersionRange,
            StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Recorder package version range must match the bundled compatibility metadata.",
                nameof(recorderPackageVersionRange));
        }

        var requiresResolvedVersion = state is GameViewRecordingCompatibilityState.Supported
            or GameViewRecordingCompatibilityState.Unsupported;
        if (requiresResolvedVersion != (resolvedVersion is not null))
        {
            throw new ArgumentException(
                "Supported and unsupported compatibility states must carry the resolved Recorder version.",
                nameof(resolvedVersion));
        }

        State = state;
        RecorderPackageVersionRange = recorderPackageVersionRange;
        ResolvedVersion = resolvedVersion is null
            ? null
            : ContractArgumentGuard.RequireValue(resolvedVersion, nameof(resolvedVersion));
    }

    public GameViewRecordingCompatibilityState State { get; }

    public string RecorderPackageVersionRange { get; }

    public string? ResolvedVersion { get; }
}

/// <summary>Describes Recorder adapter registration in an observed Unity runtime.</summary>
public sealed record GameViewRecordingAdapterCapability
{
    [JsonConstructor]
    public GameViewRecordingAdapterCapability (
        GameViewRecordingAdapterState state,
        string? adapterId,
        string? adapterVersion)
    {
        if (!TextVocabulary.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Recorder adapter state must be defined.");
        }

        var registered = state == GameViewRecordingAdapterState.Registered;
        if (registered != (adapterId is not null && adapterVersion is not null)
            || (!registered && (adapterId is not null || adapterVersion is not null)))
        {
            throw new ArgumentException(
                "Only a registered adapter state may carry adapter identity and version.",
                nameof(adapterId));
        }

        if (registered
            && (!string.Equals(adapterId, GameViewRecorderCompatibilityMetadata.AdapterId, StringComparison.Ordinal)
                || !string.Equals(adapterVersion, GameViewRecorderCompatibilityMetadata.AdapterVersion, StringComparison.Ordinal)))
        {
            throw new ArgumentException("Registered adapter identity must match the bundled compatibility metadata.", nameof(adapterId));
        }

        State = state;
        AdapterId = adapterId;
        AdapterVersion = adapterVersion;
    }

    public GameViewRecordingAdapterState State { get; }

    public string? AdapterId { get; }

    public string? AdapterVersion { get; }
}

/// <summary>Describes whether the current Unity runtime can admit a recording start.</summary>
public sealed record GameViewRecordingRuntimeAdmission
{
    [JsonConstructor]
    public GameViewRecordingRuntimeAdmission (
        GameViewRecordingRuntimeAdmissionState state,
        IReadOnlyList<UcliCode> blockingCodes)
    {
        if (!TextVocabulary.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, "Recording runtime-admission state must be defined.");
        }

        var codes = ContractArgumentGuard.RequireItems(blockingCodes, nameof(blockingCodes));
        if ((state == GameViewRecordingRuntimeAdmissionState.Ready) != (codes.Count == 0))
        {
            throw new ArgumentException(
                "A ready runtime has no blocking codes; every other runtime state must explain why it is not ready.",
                nameof(blockingCodes));
        }

        State = state;
        BlockingCodes = codes;
    }

    public GameViewRecordingRuntimeAdmissionState State { get; }

    public IReadOnlyList<UcliCode> BlockingCodes { get; }
}

/// <summary>Defines the numeric bounds published by a registered Recorder adapter.</summary>
public sealed record GameViewRecordingLimits
{
    [JsonConstructor]
    public GameViewRecordingLimits (
        int minimumWidth,
        int maximumWidth,
        int minimumHeight,
        int maximumHeight,
        int dimensionMultiple,
        int minimumFrameRate,
        int maximumFrameRate,
        int defaultMaxDurationSeconds,
        int maximumMaxDurationSeconds)
    {
        DimensionMultiple = ContractArgumentGuard.RequirePositive(
            dimensionMultiple,
            nameof(dimensionMultiple));
        MinimumWidth = RequirePositiveMultiple(
            minimumWidth,
            DimensionMultiple,
            nameof(minimumWidth));
        MaximumWidth = RequirePositiveMultiple(
            maximumWidth,
            DimensionMultiple,
            nameof(maximumWidth));
        MinimumHeight = RequirePositiveMultiple(
            minimumHeight,
            DimensionMultiple,
            nameof(minimumHeight));
        MaximumHeight = RequirePositiveMultiple(
            maximumHeight,
            DimensionMultiple,
            nameof(maximumHeight));
        MinimumFrameRate = ContractArgumentGuard.RequirePositive(minimumFrameRate, nameof(minimumFrameRate));
        MaximumFrameRate = ContractArgumentGuard.RequirePositive(maximumFrameRate, nameof(maximumFrameRate));
        DefaultMaxDurationSeconds = ContractArgumentGuard.RequirePositive(
            defaultMaxDurationSeconds,
            nameof(defaultMaxDurationSeconds));
        MaximumMaxDurationSeconds = ContractArgumentGuard.RequirePositive(
            maximumMaxDurationSeconds,
            nameof(maximumMaxDurationSeconds));

        if (MinimumWidth > MaximumWidth
            || MinimumHeight > MaximumHeight
            || MinimumFrameRate > MaximumFrameRate
            || DefaultMaxDurationSeconds > MaximumMaxDurationSeconds)
        {
            throw new ArgumentException("Recording adapter minima and defaults must not exceed their corresponding maxima.");
        }
    }

    [UcliInt32Minimum(1)]
    public int MinimumWidth { get; }

    [UcliInt32Minimum(1)]
    public int MaximumWidth { get; }

    [UcliInt32Minimum(1)]
    public int MinimumHeight { get; }

    [UcliInt32Minimum(1)]
    public int MaximumHeight { get; }

    [UcliInt32Minimum(1)]
    public int DimensionMultiple { get; }

    [UcliInt32Minimum(1)]
    public int MinimumFrameRate { get; }

    [UcliInt32Minimum(1)]
    public int MaximumFrameRate { get; }

    [UcliInt32Minimum(1)]
    public int DefaultMaxDurationSeconds { get; }

    [UcliInt32Minimum(1)]
    public int MaximumMaxDurationSeconds { get; }

    private static int RequirePositiveMultiple (
        int value,
        int dimensionMultiple,
        string parameterName)
    {
        if (value <= 0 || value % dimensionMultiple != 0)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Recording dimension limits must be positive multiples of dimensionMultiple.");
        }

        return value;
    }
}

/// <summary>Defines the fixed output and timing profile published by a registered adapter.</summary>
public sealed record GameViewRecordingCaptureProfile
{
    [JsonConstructor]
    public GameViewRecordingCaptureProfile (
        GameViewRecordingContainer container,
        GameViewRecordingCodec codec,
        bool audio,
        bool alpha,
        string encodingProfile,
        string encodingQuality,
        GameViewRecordingTimingMode timingMode)
    {
        if (container != GameViewRecordingContainer.Mp4)
        {
            throw new ArgumentOutOfRangeException(nameof(container), container, "GameView recording container must be MP4.");
        }
        if (codec != GameViewRecordingCodec.H264)
        {
            throw new ArgumentOutOfRangeException(nameof(codec), codec, "GameView recording codec must be H.264.");
        }
        if (audio)
        {
            throw new ArgumentException("GameView recording does not include audio.", nameof(audio));
        }
        if (alpha)
        {
            throw new ArgumentException("GameView recording does not include alpha.", nameof(alpha));
        }
        if (timingMode != GameViewRecordingTimingMode.ConstantFrameRateCapture)
        {
            throw new ArgumentOutOfRangeException(nameof(timingMode), timingMode, "GameView recording uses constant-frame-rate capture.");
        }

        Container = container;
        Codec = codec;
        Audio = audio;
        Alpha = alpha;
        EncodingProfile = ContractArgumentGuard.RequireValue(encodingProfile, nameof(encodingProfile));
        EncodingQuality = ContractArgumentGuard.RequireValue(encodingQuality, nameof(encodingQuality));
        TimingMode = timingMode;
    }

    public GameViewRecordingContainer Container { get; }

    public GameViewRecordingCodec Codec { get; }

    [UcliBooleanConstant(false)]
    public bool Audio { get; }

    [UcliBooleanConstant(false)]
    public bool Alpha { get; }

    public string EncodingProfile { get; }

    public string EncodingQuality { get; }

    public GameViewRecordingTimingMode TimingMode { get; }
}

/// <summary>Combines package, compatibility, adapter, and current runtime observations for recording.</summary>
public sealed record GameViewRecordingCapability
{
    [JsonConstructor]
    public GameViewRecordingCapability (
        GameViewRecordingPackageCapability package,
        GameViewRecordingCompatibilityCapability compatibility,
        GameViewRecordingAdapterCapability adapter,
        GameViewRecordingRuntimeAdmission runtimeAdmission,
        GameViewRecordingLimits? limits,
        GameViewRecordingCaptureProfile? captureProfile)
    {
        Package = package ?? throw new ArgumentNullException(nameof(package));
        Compatibility = compatibility ?? throw new ArgumentNullException(nameof(compatibility));
        Adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        RuntimeAdmission = runtimeAdmission ?? throw new ArgumentNullException(nameof(runtimeAdmission));

        ValidateStateCombination(package, compatibility, adapter);

        var registered = adapter.State == GameViewRecordingAdapterState.Registered;
        if (registered != (limits is not null && captureProfile is not null)
            || (!registered && (limits is not null || captureProfile is not null)))
        {
            throw new ArgumentException(
                "Only a registered adapter must publish both recording limits and capture profile.",
                nameof(limits));
        }
        if (runtimeAdmission.State == GameViewRecordingRuntimeAdmissionState.Ready && !registered)
        {
            throw new ArgumentException("Runtime admission cannot be ready without a registered adapter.", nameof(runtimeAdmission));
        }

        Limits = limits;
        CaptureProfile = captureProfile;
    }

    public GameViewRecordingPackageCapability Package { get; }

    public GameViewRecordingCompatibilityCapability Compatibility { get; }

    public GameViewRecordingAdapterCapability Adapter { get; }

    public GameViewRecordingRuntimeAdmission RuntimeAdmission { get; }

    public GameViewRecordingLimits? Limits { get; }

    public GameViewRecordingCaptureProfile? CaptureProfile { get; }

    private static void ValidateStateCombination (
        GameViewRecordingPackageCapability package,
        GameViewRecordingCompatibilityCapability compatibility,
        GameViewRecordingAdapterCapability adapter)
    {
        var valid = (package.State, compatibility.State, adapter.State) switch
        {
            (GameViewRecordingPackageState.Missing,
                GameViewRecordingCompatibilityState.NotApplicable,
                GameViewRecordingAdapterState.NotApplicable) => true,
            (GameViewRecordingPackageState.Indeterminate,
                GameViewRecordingCompatibilityState.Indeterminate,
                GameViewRecordingAdapterState.Unobserved) => true,
            (GameViewRecordingPackageState.Resolved,
                GameViewRecordingCompatibilityState.Unsupported,
                GameViewRecordingAdapterState.NotApplicable) => true,
            (GameViewRecordingPackageState.Resolved,
                GameViewRecordingCompatibilityState.Indeterminate,
                GameViewRecordingAdapterState.Unobserved) => true,
            (GameViewRecordingPackageState.Resolved,
                GameViewRecordingCompatibilityState.Supported,
                GameViewRecordingAdapterState.Unobserved
                    or GameViewRecordingAdapterState.Missing
                    or GameViewRecordingAdapterState.Registered) => true,
            _ => false,
        };

        if (!valid)
        {
            throw new ArgumentException("Recording capability states do not form a supported observation combination.");
        }

        if (package.State == GameViewRecordingPackageState.Resolved
            && compatibility.ResolvedVersion is not null
            && !string.Equals(package.Version, compatibility.ResolvedVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("Package and compatibility observations must identify the same Recorder version.");
        }
    }
}
