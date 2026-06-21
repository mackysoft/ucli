using System;
using System.Collections.Generic;

namespace MackySoft.Ucli.Unity.Ipc
{
    /// <summary> Tracks sensitive values that must be redacted from Unity-log IPC snapshots while a scoped operation runs. </summary>
    internal sealed class UnityLogRedactionScopeProvider
    {
        private readonly object syncRoot = new object();

        private readonly List<string> scopedValues = new List<string>();

        private string[] orderedValues = Array.Empty<string>();

        public IDisposable BeginScope (IEnumerable<string>? redactionValues)
        {
            var values = SensitiveValueRedactor.CreateOrderedValues(redactionValues);
            if (values.Length == 0)
            {
                return NoOpScope.Instance;
            }

            lock (syncRoot)
            {
                scopedValues.AddRange(values);
                RebuildOrderedValues();
            }

            return new RedactionScope(this, values);
        }

        public string Redact (string value)
        {
            string[] snapshot;
            lock (syncRoot)
            {
                snapshot = orderedValues;
            }

            return SensitiveValueRedactor.Redact(value, snapshot);
        }

        public string? RedactOrNull (string? value)
        {
            string[] snapshot;
            lock (syncRoot)
            {
                snapshot = orderedValues;
            }

            return SensitiveValueRedactor.RedactOrNull(value, snapshot);
        }

        private void EndScope (IReadOnlyList<string> values)
        {
            lock (syncRoot)
            {
                for (var i = 0; i < values.Count; i++)
                {
                    scopedValues.Remove(values[i]);
                }

                RebuildOrderedValues();
            }
        }

        private void RebuildOrderedValues ()
        {
            orderedValues = SensitiveValueRedactor.CreateOrderedValues(scopedValues);
        }

        private sealed class RedactionScope : IDisposable
        {
            private readonly UnityLogRedactionScopeProvider owner;

            private readonly string[] values;

            private bool isDisposed;

            public RedactionScope (
                UnityLogRedactionScopeProvider owner,
                string[] values)
            {
                this.owner = owner;
                this.values = values;
            }

            public void Dispose ()
            {
                if (isDisposed)
                {
                    return;
                }

                isDisposed = true;
                owner.EndScope(values);
            }
        }

        private sealed class NoOpScope : IDisposable
        {
            public static readonly NoOpScope Instance = new NoOpScope();

            public void Dispose ()
            {
            }
        }
    }
}
