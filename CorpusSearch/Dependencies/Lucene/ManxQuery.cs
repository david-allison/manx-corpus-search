using CorpusSearch.Service;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Util;
using Lucene.Net.Util.Automaton;
using System.Collections.Generic;
using System.Linq;

namespace CorpusSearch.Dependencies.Lucene
{
    public class ManxQuery(Term term) : AutomatonQuery(GetTerm(term), ToAutomaton(term))
    {
        private static Term GetTerm(Term term)
        {
            return new Term(term.Field, DiacriticService.Replace(term.Bytes.Utf8ToString()));
        }

        /// <summary>
        /// Returns the pattern term.
        /// </summary>
        public virtual Term Term => base.m_term;

        private static Automaton ToAutomaton(Term term)
        {
            IList<Automaton> automata = new List<Automaton>();

            string wildcardText = term.Text();

            for (int i = 0; i < wildcardText.Length; i++)
            {
                char c = wildcardText[i];

                // any string
                if (c ==  '*')
                {
                    automata.Add(BasicAutomata.MakeAnyString());
                    continue;
                }
                
                // single char
                if (c ==  '_')
                {
                    automata.Add(BasicAutomata.MakeAnyChar());
                    continue;
                }


                if (c == '+')
                {
                    automata.Add(BasicAutomata.MakeAnyChar());
                    automata.Add(BasicAutomata.MakeAnyString());
                    continue;
                }

                var replacementArr = DiacriticService.Replace(c);
                if (replacementArr != null)
                {
                    var replacements = replacementArr.Concat(new string[] { c.ToString() });
                    automata.Add(BasicAutomata.MakeStringUnion(replacements.Select(x => new BytesRef(x)).ToArray()));
                }
                else
                {
                    automata.Add(BasicAutomata.MakeChar(c));
                }
            }

            // allow any number of trailing question marks
            automata.Add(BasicOperations.Optional(BasicOperations.Repeat(BasicAutomata.MakeChar('?'))));

            // allow a trailing dash: 'da cre-erbee' should match 'da cre'
            var dashThenAnyChar = BasicOperations.Concatenate(BasicAutomata.MakeChar('-'), BasicAutomata.MakeAnyString());

            automata.Add(BasicOperations.Optional(dashThenAnyChar));

            var ret = BasicOperations.Concatenate(automata);

            return ret;
        }
    }
}
