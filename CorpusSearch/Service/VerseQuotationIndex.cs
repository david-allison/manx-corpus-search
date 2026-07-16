using System.Collections.Generic;
using System.Linq;
using CorpusSearch.Model;

namespace CorpusSearch.Service;

/// <summary>A dictionary whose entry text quotes scripture: what the reverse
/// verse lookup (a corpus verse line → the entries quoting it) reads. The
/// text is the full entry text, never a basic gloss — the citations ride the
/// quotations the gloss drops.</summary>
public interface IQuotingDictionary
{
    /// <summary>Every entry as (headword, the text its citations live in)</summary>
    IEnumerable<(string Word, string Text)> QuotableEntries { get; }
}

/// <summary>One dictionary entry quoting a verse: "aalid (Cregeen), via
/// 'Ps. 45, 12'". The client links the word into the dictionary.</summary>
public sealed record VerseQuotation(string Dictionary, string Slug, string Word, string Citation);

/// <summary>
/// The reverse of a dictionary entry's verse citations (see
/// <see cref="VerseCitations"/>): canonical verse key → the entries quoting
/// that verse, so a corpus verse line can point back into the books
/// (quotes.nvh direction 2). Built once, on first use.
/// </summary>
public class VerseQuotationIndex
{
    private readonly Dictionary<string, List<VerseQuotation>> byKey = [];

    public VerseQuotationIndex(IEnumerable<ISearchDictionary> dictionaries)
    {
        foreach (var dictionary in dictionaries)
        {
            if (dictionary is not IQuotingDictionary quoting)
            {
                continue;
            }
            foreach (var (word, text) in quoting.QuotableEntries)
            {
                foreach (var citation in VerseCitations.FindAll(text) ?? [])
                {
                    if (!byKey.TryGetValue(citation.Key, out var quotations))
                    {
                        byKey[citation.Key] = quotations = [];
                    }
                    // an entry may print the same verse twice: one row each
                    if (!quotations.Any(x => x.Slug == dictionary.Slug && x.Word == word))
                    {
                        quotations.Add(new VerseQuotation(
                            dictionary.Identifier, dictionary.Slug, word, citation.Text));
                    }
                }
            }
        }
    }

    /// <summary>The entries quoting the verse; null when none (kept off the wire)</summary>
    public List<VerseQuotation>? For(string key) => byKey.GetValueOrDefault(key);
}
