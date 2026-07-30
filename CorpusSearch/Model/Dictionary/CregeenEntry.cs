using System;
using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Model.Dictionary;

/// <summary>
/// JSON Model for the cregeen dictionary
/// </summary>
public class CregeenEntry
{
    public required List<string> Words { get; set; }
    public required string EntryHtml { get; set; }
    /// <summary>Null in the filtered views built by <see cref="FilterTo"/></summary>
    public string? Definition { get; set; }
    public List<string>? PartsOfSpeech { get; set; }
    public List<string>? Gender { get; set; }
    /// <summary>Editorial notes from cregeen-nvh; a "gender:" note records
    /// corpus evidence against the printed gender</summary>
    public string? Notes { get; set; }
    public required string HeadingHtml { get; set; }
    public List<CregeenEntry>? Children { get; set; }

    /// <summary>The printed headword this node stands for, particle and all:
    /// the entry's identity, where <see cref="Words"/> is its search bag.
    /// Null on data files older than the field</summary>
    public string? Headword { get; set; }
    /// <summary>The grammar words the book sets in italic before the bold
    /// headword ("e hardjyn": "e")</summary>
    public string? Particle { get; set; }
    /// <summary>The letter section the book files the entry under, which for
    /// a mutated head is not its spelling's ("e hardjyn" files under A,
    /// beside ardjyn)</summary>
    public string? Letter { get; set; }
    /// <summary>The radical's initial, on entries whose own spelling files
    /// away from it</summary>
    public string? RadicalInitial { get; set; }

    public List<CregeenEntry> SafeChildren => Children ?? [];

    public List<CregeenEntry> ChildrenRecursive => new[] { this }.Concat(SafeChildren.SelectMany(x => x.ChildrenRecursive)).ToList();

    public bool ContainsWordExact(string word)
    {
        return Words.Any(x => x == word);
    }

    public IList<CregeenEntry> FilterTo(string search)
    {
        if (ContainsWordExact(search))
        {
            return new[] { this };
        }

        var children = SafeChildren.SelectMany(x => x.FilterTo(search)).ToList();

        if (!children.Any())
        {
            return Array.Empty<CregeenEntry>();
        }

        return new[]
        {
            new CregeenEntry
            {
                Words = this.Words,
                EntryHtml = this.EntryHtml,
                HeadingHtml = this.HeadingHtml,
                Children = children,
            }
        };
    }
}