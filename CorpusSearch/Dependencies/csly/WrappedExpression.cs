using CorpusSearch.Dependencies.csly.Model;

namespace CorpusSearch.Dependencies.csly
{
    internal class WrappedExpression(string v1, Expression middle, string v2) : Expression("wrapped")
    {
        public Expression Wrapped { get;  } = middle;


        public override string ToString()
        {
            return $"{v1} {Wrapped} {v2}";
        }
    }
}