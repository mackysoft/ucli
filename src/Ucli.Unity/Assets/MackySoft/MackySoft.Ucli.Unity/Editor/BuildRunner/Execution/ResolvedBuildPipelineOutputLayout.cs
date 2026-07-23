using System;
using MackySoft.FileSystem;
using MackySoft.Ucli.Contracts.Ipc;
using MackySoft.Ucli.Contracts.Text;

#nullable enable

namespace MackySoft.Ucli.Unity.Build
{
    /// <summary>
    /// Carries a BuildPipeline output shape and its guarded absolute location after request or product-policy resolution.
    /// </summary>
    internal sealed class ResolvedBuildPipelineOutputLayout
    {
        /// <summary> Initializes one resolved BuildPipeline output layout. </summary>
        /// <param name="shape"> The expected filesystem node shape. </param>
        /// <param name="locationPath"> The guarded absolute BuildPipeline output location. </param>
        /// <exception cref="ArgumentOutOfRangeException"> Thrown when <paramref name="shape" /> is not defined. </exception>
        /// <exception cref="ArgumentNullException"> Thrown when <paramref name="locationPath" /> is <see langword="null" />. </exception>
        public ResolvedBuildPipelineOutputLayout (
            IpcBuildOutputLayoutShape shape,
            AbsolutePath locationPath)
        {
            if (!ContractLiteralCodec.IsDefined(shape))
            {
                throw new ArgumentOutOfRangeException(nameof(shape), shape, "Build output layout shape must be specified.");
            }

            Shape = shape;
            LocationPath = locationPath ?? throw new ArgumentNullException(nameof(locationPath));
        }

        /// <summary> Gets the expected filesystem node shape. </summary>
        public IpcBuildOutputLayoutShape Shape { get; }

        /// <summary> Gets the guarded absolute BuildPipeline output location. </summary>
        public AbsolutePath LocationPath { get; }

        /// <summary> Converts this guarded layout at the IPC response boundary. </summary>
        public IpcBuildOutputLayout ToContract ()
        {
            return new IpcBuildOutputLayout(Shape, LocationPath.Value);
        }
    }
}
