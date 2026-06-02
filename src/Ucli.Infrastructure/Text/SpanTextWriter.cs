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

    /// <summary> Appends one boolean value using lowercase JSON-compatible text. </summary>
    public void AppendBool (bool value)
    {
        Append(value ? "true" : "false");
    }

    /// <summary> Appends one optional string value prefixed by a label. </summary>
    public void AppendOptional (
        string prefix,
        string? value)
    {
        if (value == null)
        {
            return;
        }

        Append(prefix);
        Append(value);
    }

    /// <summary> Appends one optional integer value prefixed by a label. </summary>
    public void AppendOptionalInvariant (
        string prefix,
        int? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        Append(prefix);
        AppendInvariant(value.Value);
    }

    /// <summary> Appends one optional integer value prefixed by a label. </summary>
    public void AppendOptionalInvariant (
        string prefix,
        long? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        Append(prefix);
        AppendInvariant(value.Value);
    }

    /// <summary> Appends one optional boolean value prefixed by a label. </summary>
    public void AppendOptionalBool (
        string prefix,
        bool? value)
    {
        if (!value.HasValue)
        {
            return;
        }

        Append(prefix);
        AppendBool(value.Value);
    }
}
