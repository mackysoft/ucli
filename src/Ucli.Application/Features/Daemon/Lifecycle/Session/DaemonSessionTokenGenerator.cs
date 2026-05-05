using System.Security.Cryptography;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandContracts;
using MackySoft.Ucli.Application.Features.Daemon.Common.CommandExecution;
using MackySoft.Ucli.Application.Features.Daemon.Common.Projection;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Cleanup;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Diagnosis;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Process;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Start;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Status;
using MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Stop;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Common;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Daemon;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Streaming;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Unity;
using MackySoft.Ucli.Application.Features.Daemon.Observability.Logs.Validation;
using MackySoft.Ucli.Contracts.Text;

namespace MackySoft.Ucli.Application.Features.Daemon.Lifecycle.Session;

/// <summary> Implements cryptographically random daemon session token generation. </summary>
internal sealed class DaemonSessionTokenGenerator : IDaemonSessionTokenGenerator
{
    /// <summary> Creates one daemon session token. </summary>
    /// <returns> The created daemon session token value. </returns>
    public string Create ()
    {
        Span<byte> tokenBuffer = stackalloc byte[32];
        RandomNumberGenerator.Fill(tokenBuffer);
        return Base64UrlCodec.Encode(tokenBuffer);
    }
}
