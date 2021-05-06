using System.Collections.Generic;
using System.Linq;

namespace Codex_API.Dependencies.csly
{
    public class AdjacentWordExpression : Expression
    {
        private readonly List<string> words;

        public AdjacentWordExpression(IEnumerable<string> words)
            : base("words")
        {
            this.words = words.ToList();
        }

        public IEnumerable<string> Words => words;

        public override string ToString()
        {
            return $"'{string.Join(",", words)}'";
        }
    }
}
