using MackySoft.Ucli.Contracts.Assurance.Build;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Assurance.Build.Profiles;

/// <summary> Represents build inputs resolved from a build profile. </summary>
internal abstract class ResolvedBuildInputs
{
    private ResolvedBuildInputs ()
    {
    }

    /// <summary> Gets the build input kind represented by this variant. </summary>
    public abstract BuildProfileInputsKind Kind { get; }

    /// <summary> Represents build inputs declared directly in the build profile. </summary>
    public sealed class Explicit : ResolvedBuildInputs
    {
        /// <summary> Initializes explicit build inputs. </summary>
        public Explicit (
            BuildTargetStableName buildTarget,
            ResolvedBuildScenes scenes,
            ResolvedBuildOptions options)
        {
            if (!TextVocabulary.IsDefined(buildTarget))
            {
                throw new ArgumentOutOfRangeException(nameof(buildTarget), buildTarget, "Build target must be defined.");
            }

            BuildTarget = buildTarget;
            Scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <inheritdoc />
        public override BuildProfileInputsKind Kind => BuildProfileInputsKind.Explicit;

        /// <summary> Gets the build target. </summary>
        public BuildTargetStableName BuildTarget { get; }

        /// <summary> Gets the scene selection. </summary>
        public ResolvedBuildScenes Scenes { get; }

        /// <summary> Gets the build options. </summary>
        public ResolvedBuildOptions Options { get; }
    }

    /// <summary> Represents inputs selected from a Unity Build Profile asset. </summary>
    public sealed class UnityBuildProfile : ResolvedBuildInputs
    {
        /// <summary> Initializes Unity Build Profile inputs. </summary>
        public UnityBuildProfile (UnityBuildProfileAssetPath path)
        {
            Path = path ?? throw new ArgumentNullException(nameof(path));
        }

        /// <inheritdoc />
        public override BuildProfileInputsKind Kind => BuildProfileInputsKind.UnityBuildProfile;

        /// <summary> Gets the Unity Build Profile asset path. </summary>
        public UnityBuildProfileAssetPath Path { get; }
    }
}
