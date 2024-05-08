namespace CorpusSearch.Dependencies.csly.Model
{
    public class AndExpression(Expression left, Expression right) : Expression("and")
    {
        public Expression Left { get; } = left;
        public Expression Right { get; } = right;

        public override string ToString()
        {
            return $"{Left} {base.ToString()} {Right}";
        }
    }
}
