using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Dependencies.csly.Model
{
    public class AdjacentWordExpression(IEnumerable<string> words) : Expression("words")
    {
        private readonly List<string> words = words.ToList();

        public IEnumerable<string> Words => words;

        public override string ToString()
        {
            return $"'{string.Join(",", words)}'";
        }
    }
}
