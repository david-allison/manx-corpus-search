using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Model.Dictionary;

/**
 * Keep in sync with kelly-m2e-manx-dictionary-data: KellyManxToEnglishEntry
 */
public class KellyManxToEnglishEntry
{
    public required List<string> Words { get; set; }
    public required string Definition { get; set; }
    /// <summary>Plural forms split out of the printed definition ("s. pl. BILJIN.")</summary>
    public List<string>? Plurals { get; set; } = [];
    public List<KellyManxToEnglishEntry>? Children { get; set; } = [];
    
    // Added
    public List<KellyManxToEnglishEntry> SafeChildren => Children ?? [];
    public List<KellyManxToEnglishEntry> ChildrenRecursive => new[] { this }.Concat(SafeChildren.SelectMany(x => x.ChildrenRecursive)).ToList();
}