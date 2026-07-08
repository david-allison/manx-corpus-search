using System;
using System.Collections.Generic;
using System.Text;

namespace CorpusSearch.Model;

/// <summary>
/// A string produced by transforming a source string, retaining for each output character
/// the index of the source character which produced it.
/// Allows mapping a match offset in normalized text back to a range in the original text.
/// </summary>
public sealed class MappedText
{
    /// <summary>Source index of characters with no originating source character (e.g. padding)</summary>
    private const int NoSource = -1;

    public string Text { get; }

    /// <summary>For each character of <see cref="Text"/>: the index in the original source string</summary>
    private readonly int[] _sourceIndices;

    private MappedText(string text, int[] sourceIndices)
    {
        Text = text;
        _sourceIndices = sourceIndices;
    }

    public static MappedText Identity(string source)
    {
        var indices = new int[source.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            indices[i] = i;
        }
        return new MappedText(source, indices);
    }

    /// <summary>Applies a per-character transformation: each character maps to 0..n characters</summary>
    public MappedText MapChars(Func<char, string> mapper)
    {
        var text = new StringBuilder(Text.Length);
        var indices = new List<int>(Text.Length);
        for (int i = 0; i < Text.Length; i++)
        {
            string replacement = mapper(Text[i]);
            text.Append(replacement);
            for (int j = 0; j < replacement.Length; j++)
            {
                indices.Add(_sourceIndices[i]);
            }
        }
        return new MappedText(text.ToString(), indices.ToArray());
    }

    /// <summary>
    /// Replaces the text with an equal-length transformation of it (e.g. lower-casing),
    /// keeping the existing mapping
    /// </summary>
    /// <exception cref="ArgumentException">if the length differs</exception>
    public MappedText ReplaceTextSameLength(string newText)
    {
        if (newText.Length != Text.Length)
        {
            throw new ArgumentException($"expected length {Text.Length}, got {newText.Length}", nameof(newText));
        }
        return new MappedText(newText, _sourceIndices);
    }

    /// <summary>Equivalent of <see cref="string.Trim()"/></summary>
    public MappedText Trim()
    {
        int start = 0;
        while (start < Text.Length && char.IsWhiteSpace(Text[start]))
        {
            start++;
        }
        int end = Text.Length;
        while (end > start && char.IsWhiteSpace(Text[end - 1]))
        {
            end--;
        }
        return new MappedText(Text[start..end], _sourceIndices[start..end]);
    }

    /// <summary>Surrounds the text with strings which map to no source character</summary>
    public MappedText Pad(string prefix, string suffix)
    {
        var indices = new int[prefix.Length + _sourceIndices.Length + suffix.Length];
        Array.Fill(indices, NoSource, 0, prefix.Length);
        Array.Copy(_sourceIndices, 0, indices, prefix.Length, _sourceIndices.Length);
        Array.Fill(indices, NoSource, prefix.Length + _sourceIndices.Length, suffix.Length);
        return new MappedText(prefix + Text + suffix, indices);
    }

    /// <summary>
    /// Maps [start, end) in <see cref="Text"/> to the range in the source string which produced it.
    /// </summary>
    /// <returns>null if the range is empty, out of bounds, or covers no source character</returns>
    public (int Start, int End)? MapRangeToSource(int start, int end)
    {
        start = Math.Max(start, 0);
        end = Math.Min(end, Text.Length);
        if (start >= end)
        {
            return null;
        }

        // transformations never reorder characters, so the first/last mapped characters bound the range
        int first = start;
        while (first < end && _sourceIndices[first] == NoSource)
        {
            first++;
        }
        if (first == end)
        {
            return null;
        }
        int last = end - 1;
        while (_sourceIndices[last] == NoSource)
        {
            last--;
        }

        return (_sourceIndices[first], _sourceIndices[last] + 1);
    }
}
