#nullable disable // not yet migrated, see the .csproj
using System.Collections.Generic;
using Lucene.Net.Index;
using Lucene.Net.Search;

namespace CorpusSearch.Dependencies.Lucene;

public static class TermVectorOffsetReader
{
    /// <summary>
    /// Reads the term vector of a document: the character offsets of the token at each token
    /// position. The offsets refer to the text of the analyzed field (the padded normalized line,
    /// see <see cref="Model.DocumentLine.NormalizedManx"/>).
    /// </summary>
    /// <returns>token position -> character offsets, or null if the document has no term vector
    /// with positions and offsets</returns>
    public static Dictionary<int, (int Start, int End)> GetPositionOffsets(IndexReader reader, int docId, string field)
    {
        Terms termVector = reader.GetTermVector(docId, field);
        if (termVector is not { HasPositions: true, HasOffsets: true })
        {
            return null;
        }

        var result = new Dictionary<int, (int Start, int End)>();
        TermsEnum terms = termVector.GetEnumerator();
        DocsAndPositionsEnum positions = null;
        while (terms.MoveNext())
        {
            positions = terms.DocsAndPositions(null, positions);
            if (positions == null || positions.NextDoc() == DocIdSetIterator.NO_MORE_DOCS)
            {
                continue;
            }
            for (int i = 0; i < positions.Freq; i++)
            {
                int position = positions.NextPosition();
                result[position] = (positions.StartOffset, positions.EndOffset);
            }
        }
        return result;
    }
}
