using System;
using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Model.Dictionary
{
    /// <summary>
    /// JSON Model for the cregeen dictionary
    /// </summary>
    public class CregeenEntry
    {
        public List<string> Words { get; set; }
        public string EntryHtml { get; set; }
        public string Definition { get; set; }
        public List<string> PartsOfSpeech { get; set; }
        public List<string> Gender { get; set; }
        public string HeadingHtml { get; set; }
        // nullable
        public List<CregeenEntry> Children { get; set; }

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
}
