using System.Globalization;

namespace MackySoft.Ucli.Infrastructure.Text;

/// <summary> Writes small fixed-size text fragments into a caller-provided character span. </summary>
internal ref struct SpanTextWriter
{
    private readonly Span<char> destination;
    private int offset;

    /// <summary> Initializes a new instance of the <see cref="SpanTextWriter" /> struct. </summary>
    public SpanTextWriter (Span<char> destination)
    {
        this.destination = destination;
        offset = 0;
    }

    /// <summary> Appends one character. </summary>
    public void Append (char value)
    {
        destination[offset++] = value;
    }

    /// <summary> Appends one string value. </summary>
    public void Append (string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        value.AsSpan().CopyTo(destination.Slice(offset));
        offset += value.Length;
    }

    /// <summary> Appends one integer using invariant-culture formatting. </summary>
    public void AppendInvariant (long value)
    {
        if (!value.TryFormat(
                destination.Slice(offset),
                out var written,
                provider: CultureInfo.InvariantCulture))
        {
            throw new InvalidOperationException("Text buffer is too small for invariant integer formatting.");
        }

        offset += written;
    }
}
