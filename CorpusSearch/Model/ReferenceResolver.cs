using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace CorpusSearch.Model;

/// <summary>
/// Derives each line's <see cref="DocumentLine.CanonicalReference"/> from the
/// opaque <see cref="DocumentLine.Reference"/> strings that inline extraction (or
/// an explicit Reference column) produced. Walks the lines in document order,
/// because a bare verse number ("[3]", "8.") only means anything under the last
/// chapter heading; self-contained forms (Genesis:1:1, MS 1 Thessalonians 2.16)
/// never touch that context, so a stray citation cannot leak into following
/// verses. Only ever writes CanonicalReference — the reference text, the Manx
/// text and every token stream are untouched by construction.
/// </summary>
public static class ReferenceResolver
{
    private const RegexOptions Options =
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant;

    // Mian:1:1 / Arrane Solomon:2:3 (the P Kelly Bible import, already ':'-joined)
    private static readonly Regex ColonVerse =
        new(@"^(?<book>[^:]{1,40}):(?<ch>\d{1,3}):(?<v>\d{1,3})$", Options);

    // 3 — a verse number whose chapter is whatever the last heading said
    private static readonly Regex BareNumber = new(@"^\d{1,3}$", Options);

    // CAB. II. / Cabdil 4 / CHAPTER V — a chapter heading that trusts the document
    // to know its book (the manifest's referenceBook)
    private static readonly Regex ChapterHeading =
        new(@"^(?:cab\.?|cabdil|chapter)\s+(?<n>\d{1,3}|[ivxlcdm]{1,9})\.?$", Options);

    // PSALM 23 / Psalm cl — a heading that names its own book
    private static readonly Regex PsalmHeading =
        new(@"^psalms?\s+(?<n>\d{1,3}|[ivxlcdm]{1,9})\.?$", Options);

    // Beatus vir qui non abiit. psal. 1 (Phillips 1610, trailing) /
    // Psal. 1. Beatus vir, qui non abiit (the 1765 psalter, leading)
    private static readonly Regex PsalmIncipit =
        new(@"(?:^psal\.?\s*(?<n>\d{1,3})\b|\bpsal\.?\s*(?<n>\d{1,3})$)", Options);

    // jud xii 6 / ms 1 thessalonians 2 16 / isa xlv 24 25 — after normalization;
    // a verse range or list (24, 25 / 9–21) keeps its first verse
    private static readonly Regex CitationShape =
        new(@"^(?<head>.+?) (?<ch>\d{1,3}|[ivxlcdm]{1,9}) (?<v>\d{1,3})(?: ?[-– ] ?\d{1,3})*$", Options);

    public static void Resolve(Document document, IEnumerable<DocumentLine> lines)
    {
        var contextBook = BibleBooks.Find(document.ReferenceBook);
        int? contextChapter = null;
        var psalms = BibleBooks.FindById("psalms")!;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line.Reference))
            {
                continue;
            }
            var reference = line.Reference.Trim();

            CanonicalReference? resolved;
            if (ColonVerse.Match(reference) is { Success: true } colon)
            {
                var book = BibleBooks.Find(colon.Groups["book"].Value);
                resolved = book == null
                    ? null
                    : new CanonicalReference(book,
                        int.Parse(colon.Groups["ch"].Value), int.Parse(colon.Groups["v"].Value));
            }
            else if (BareNumber.IsMatch(reference))
            {
                resolved = contextBook != null && contextChapter != null
                    ? new CanonicalReference(contextBook, contextChapter.Value, int.Parse(reference))
                    : null;
            }
            else if (ChapterHeading.Match(reference) is { Success: true } chapter)
            {
                contextChapter = Number(chapter.Groups["n"].Value);
                resolved = contextBook != null && contextChapter != null
                    ? new CanonicalReference(contextBook, contextChapter.Value)
                    : null;
            }
            else if (PsalmHeading.Match(reference) is { Success: true } psalm)
            {
                resolved = OpenPsalm(psalm);
            }
            else if (PsalmIncipit.Match(reference) is { Success: true } incipit)
            {
                resolved = OpenPsalm(incipit);
            }
            else
            {
                resolved = TryParseCitation(reference);
            }

            line.CanonicalReference = resolved?.Key;
            continue;

            CanonicalReference? OpenPsalm(Match heading)
            {
                var number = Number(heading.Groups["n"].Value);
                if (number == null)
                {
                    return null;
                }
                contextBook = psalms;
                contextChapter = number;
                return new CanonicalReference(psalms, number.Value);
            }
        }
    }

    /// <summary>
    /// A free-standing citation ("MS 1 Thessalonians 2.16", Cregeen's "Jud. xii. 6",
    /// "Luke xiii, 16"): book by any known name, chapter roman or arabic, verse
    /// arabic, punctuation as the typesetter felt. Null when the book isn't in the
    /// canon ("Methodist Hymn Book, lx. 5") — leading words are dropped one at a
    /// time until the registry recognises the rest, so prefixes like "MS" can't
    /// hide a real citation.
    /// </summary>
    public static CanonicalReference? TryParseCitation(string citation)
    {
        // editorial brackets restore elided letters (H[a]b.); periods, commas and
        // colons vary freely between printings — flatten them all to single spaces
        var normalized = citation.Replace("[", "").Replace("]", "");
        normalized = Regex.Replace(normalized, @"[\s.,:;]+", " ").Trim();

        var match = CitationShape.Match(normalized);
        if (!match.Success)
        {
            return null;
        }
        var chapter = Number(match.Groups["ch"].Value);
        if (chapter == null)
        {
            return null;
        }
        var verse = int.Parse(match.Groups["v"].Value);

        var head = match.Groups["head"].Value;
        while (true)
        {
            var book = BibleBooks.Find(head);
            if (book != null)
            {
                return new CanonicalReference(book, chapter.Value, verse);
            }
            var space = head.IndexOf(' ');
            if (space < 0)
            {
                return null;
            }
            head = head[(space + 1)..];
        }
    }

    private static int? Number(string value) =>
        int.TryParse(value, out var arabic) ? arabic : RomanNumerals.TryParse(value);
}
