using System.IO;
using Lucene.Net.Analysis.Util;
using Lucene.Net.Util;

namespace CorpusSearch.Dependencies.Lucene;

/// <summary>
/// Tokens for the reference field: runs of letters AND digits, so
/// "MS 1 Thessalonians 2.16" yields ms/1/thessalonians/2/16 and "Psalm 23" is
/// searchable. <see cref="ManxTokenizer"/> drops digits, which would make
/// verse references unfindable.
/// </summary>
public sealed class ReferenceTokenizer(LuceneVersion version, TextReader reader)
    : CharTokenizer(version, reader)
{
    protected override bool IsTokenChar(int c) => char.IsLetterOrDigit((char)c);
}
