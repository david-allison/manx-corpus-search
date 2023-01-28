using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Model.Dictionary;

/**
 * Keep in sync with kelly-m2e-manx-dictionary-data: KellyManxToEnglishEntry
 */
public class KellyManxToEnglishEntry
{
    public List<string> Words { get; set; }
    public string Definition { get; set; }
    public List<KellyManxToEnglishEntry> Children { get; set; } = new();
    
    // Added
    public List<KellyManxToEnglishEntry> SafeChildren => Children ?? new List<KellyManxToEnglishEntry>();
    public List<KellyManxToEnglishEntry> ChildrenRecursive => new[] { this }.Concat(SafeChildren.SelectMany(x => x.ChildrenRecursive)).ToList();
}