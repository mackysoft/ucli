using System;
using System.Collections;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using MackySoft.Ucli.Unity.Ipc;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace MackySoft.Ucli.Unity.Tests
{
    public sealed class FileBackedSessionTokenValidatorTests
    {
        [UnityTest]
        [Category("Size.Small")]
        public IEnumerator ValidateAsync_WhenSessionFileContentChanges_UsesReplacementToken () => UniTask.ToCoroutine(async () =>
        {
            var storageRoot = Path.Combine(Path.GetTempPath(), "ucli-file-session-token-validator-tests", Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(storageRoot);
                var sessionPath = Path.Combine(storageRoot, "session.json");
                var validator = new FileBackedSessionTokenValidator(sessionPath);
                var fixedLastWriteTimeUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                WriteSessionJson(sessionPath, "token-a", fixedLastWriteTimeUtc);

                Assert.That(
                    await validator.ValidateAsync("token-a", CancellationToken.None),
                    Is.True);

                WriteSessionJson(sessionPath, "token-b", fixedLastWriteTimeUtc);

                Assert.That(
                    await validator.ValidateAsync("token-a", CancellationToken.None),
                    Is.False);
                Assert.That(
                    await validator.ValidateAsync("token-b", CancellationToken.None),
                    Is.True);
            }
            finally
            {
                if (Directory.Exists(storageRoot))
                {
                    Directory.Delete(storageRoot, recursive: true);
                }
            }
        });

        private static void WriteSessionJson (
            string sessionPath,
            string sessionToken,
            DateTime lastWriteTimeUtc)
        {
            File.WriteAllText(sessionPath, $"{{\"sessionToken\":\"{sessionToken}\"}}");
            File.SetLastWriteTimeUtc(sessionPath, lastWriteTimeUtc);
        }
    }
}
