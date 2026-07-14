namespace CorpusSearch.Model;

/// <summary>
/// A structured scripture reference: the cross-version identity of a chapter or a
/// verse, shared by every translation of it. Travels as the key
/// "book.chapter[.verse]" ("1-thessalonians.2.16"; "psalms.23" for a chapter
/// heading row), which is what <see cref="DocumentLine.CanonicalReference"/> holds
/// and what the index matches exactly.
/// </summary>
public sealed record CanonicalReference(BibleBook Book, int Chapter, int? Verse = null)
{
    public string Key => Verse == null ? ChapterKey : $"{ChapterKey}.{Verse}";

    /// <summary>Key of the whole chapter: what a heading row carries, and what a
    /// verse falls back to when a version has no verse-level rows (the Metrical
    /// Psalms sing whole psalms).</summary>
    public string ChapterKey => $"{Book.Id}.{Chapter}";

    /// <summary>"1 Thessalonians 2:16" / "Psalms 23"</summary>
    public string Display => Verse == null
        ? $"{Book.DisplayName} {Chapter}"
        : $"{Book.DisplayName} {Chapter}:{Verse}";

    public static CanonicalReference? TryParseKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }
        var parts = key.Split('.');
        if (parts.Length is < 2 or > 3)
        {
            return null;
        }
        var book = BibleBooks.FindById(parts[0]);
        if (book == null || !int.TryParse(parts[1], out var chapter) || chapter <= 0)
        {
            return null;
        }
        if (parts.Length == 2)
        {
            return new CanonicalReference(book, chapter);
        }
        return int.TryParse(parts[2], out var verse) && verse > 0
            ? new CanonicalReference(book, chapter, verse)
            : null;
    }
}
